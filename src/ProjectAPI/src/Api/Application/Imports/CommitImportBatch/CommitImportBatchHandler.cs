using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Imports;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Imports.Entities;
using ProjectAPI.Infrastructure.Context;
using ImmeubleEntity = ProjectAPI.Domain.Immeubles.Entities.Immeuble;
using UnitEntity = ProjectAPI.Domain.Immeubles.Entities.Unit;

namespace ProjectAPI.Api.Application.Imports.CommitImportBatch;

/// <summary>
/// §5.11/§23 — the only place Buildings/Units are actually written from an
/// import. All-or-nothing: one transaction, and a fingerprint mismatch
/// (file hash, row count) throws IMPORT_SOURCE_CHANGED before touching
/// anything, per "commit refused if file/project/rows changed since
/// validation".
///
/// Guard rails (§5.11, verbatim from the spec):
///   - never sets a reserved/sold unit back to AVAILABLE;
///   - never touches a unit's contractual price fields once it has one;
///   - never deletes an entity for a row that disappeared from the file.
/// </summary>
public class CommitImportBatchHandler : IRequestHandler<CommitImportBatchCommand, CommitImportBatchResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public CommitImportBatchHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<CommitImportBatchResponse> Handle(CommitImportBatchCommand request, CancellationToken ct)
    {
        var batch = await _db.Set<ImportBatch>()
            .Include(b => b.Rows)
            .FirstOrDefaultAsync(b => b.Id == request.BatchId, ct)
            ?? throw new NotFoundException($"Import batch {request.BatchId} not found.");

        await _projectScope.EnsureProjectAccessAsync(batch.ProjectId, ct);

        if (batch.Status != ImportBatchStatus.ReadyToCommit)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.InvalidStatusTransition,
                $"Le lot doit être prêt à être importé (statut actuel : {batch.Status}).",
                StatusCodes.Status409Conflict);
        }

        // §5.11 — "commit refused if file/project/rows changed since
        // validation": re-derive the fingerprint from the bytes sent now and
        // compare against what validation recorded.
        var currentHash = Convert.ToHexString(SHA256.HashData(request.FileContent));
        if (currentHash != batch.FileHash)
        {
            throw BusinessRuleException.ImportSourceChanged();
        }

        var parsed = ImportWorkbookReader.Parse(request.FileContent);
        if (parsed.StructuralError is not null || parsed.Buildings.Count + parsed.Units.Count != batch.TotalRows)
        {
            throw BusinessRuleException.ImportSourceChanged();
        }

        batch.Status = ImportBatchStatus.Committing;
        await _db.SaveChangesAsync(ct);

        var response = new CommitImportBatchResponse { BatchId = batch.Id };

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var existingBuildings = await _db.Set<ImmeubleEntity>()
                .Where(im => im.ProjectId == batch.ProjectId)
                .ToDictionaryAsync(im => im.Name, StringComparer.OrdinalIgnoreCase, ct);

            foreach (var b in parsed.Buildings.Where(b => b.IsValid))
            {
                if (existingBuildings.ContainsKey(b.Name))
                {
                    // Existing building: import never overwrites what is
                    // already there beyond what the spec allows (name match
                    // only, no destructive update of an existing row).
                    continue;
                }

                var immeuble = new ImmeubleEntity
                {
                    Id = Guid.NewGuid(),
                    ProjectId = batch.ProjectId,
                    Name = b.Name,
                    Location = b.Location,
                    Type = b.Type,
                    ResidencyType = b.ResidencyType,
                    MinPrice = b.MinPrice,
                    MaxPrice = b.MaxPrice,
                    Latitude = b.Latitude,
                    Longitude = b.Longitude,
                    Description = b.Description,
                    Status = ProjectAPI.Domain.Construction.Entities.ProjectStatusCodes.SurPlan
                };
                _db.Add(immeuble);
                existingBuildings[b.Name] = immeuble;
                response.BuildingsCreated++;
            }

            await _db.SaveChangesAsync(ct);

            var existingUnits = await _db.Set<UnitEntity>()
                .Where(u => existingBuildings.Values.Select(im => im.Id).Contains(u.ProjectId))
                .ToListAsync(ct);

            // Floor rows are resolved/created per building the same way
            // Buildings themselves are above — one row per (building, floor
            // label), name-matched, never duplicated across re-imports.
            var existingFloors = await _db.Set<Floor>()
                .Where(f => existingBuildings.Values.Select(im => im.Id).Contains(f.ImmeubleId))
                .ToListAsync(ct);

            foreach (var u in parsed.Units.Where(u => u.IsValid))
            {
                if (!existingBuildings.TryGetValue(u.BuildingName, out var immeuble))
                {
                    // Should not happen: validation already confirmed the
                    // building resolves. Skip defensively rather than throw
                    // mid-transaction over a row that passed validation.
                    continue;
                }

                var floor = existingFloors.FirstOrDefault(f =>
                    f.ImmeubleId == immeuble.Id && string.Equals(f.Name, u.Floor, StringComparison.OrdinalIgnoreCase));
                if (floor is null)
                {
                    floor = new Floor
                    {
                        Id = Guid.NewGuid(),
                        ImmeubleId = immeuble.Id,
                        Name = u.Floor,
                        SequenceNo = existingFloors.Count(f => f.ImmeubleId == immeuble.Id),
                    };
                    _db.Add(floor);
                    existingFloors.Add(floor);
                }

                var existingUnit = existingUnits.FirstOrDefault(x =>
                    x.ProjectId == immeuble.Id &&
                    string.Equals(x.UnitNumber, u.UnitNumber, StringComparison.OrdinalIgnoreCase));

                if (existingUnit is not null)
                {
                    // §5.11 — never touch a unit that already has a
                    // contractual price, and never move it off AVAILABLE.
                    if (existingUnit.Status != UnitCommercialStatus.Available)
                    {
                        continue;
                    }

                    existingUnit.FloorId = floor.Id;
                    existingUnit.NumberOfBedrooms = u.NumberOfBedrooms;
                    existingUnit.NumberOfBathrooms = u.NumberOfBathrooms;
                    existingUnit.ApartmentSurface = u.ApartmentSurface;
                    existingUnit.BalconySurface = u.BalconySurface;
                    existingUnit.TerraceSurface = u.TerraceSurface;
                    existingUnit.GardenSurface = u.GardenSurface;
                    existingUnit.TotalSurface = u.TotalSurface;
                    existingUnit.View = u.View;
                    existingUnit.Orientation = u.Orientation;

                    if (existingUnit.LatestPrice is null && u.LatestPrice.HasValue)
                    {
                        // Only fills a price the unit never had; an existing
                        // contractual price is never overwritten by import.
                        existingUnit.LatestPrice = u.LatestPrice;
                    }

                    response.UnitsUpdated++;
                    continue;
                }

                var unit = new UnitEntity
                {
                    Id = Guid.NewGuid(),
                    ProjectId = immeuble.Id,
                    FloorId = floor.Id,
                    UnitNumber = u.UnitNumber,
                    NumberOfBedrooms = u.NumberOfBedrooms,
                    NumberOfBathrooms = u.NumberOfBathrooms,
                    ApartmentSurface = u.ApartmentSurface,
                    BalconySurface = u.BalconySurface,
                    TerraceSurface = u.TerraceSurface,
                    GardenSurface = u.GardenSurface,
                    TotalSurface = u.TotalSurface,
                    View = u.View,
                    Orientation = u.Orientation,
                    LatestPrice = u.LatestPrice,
                    Status = UnitCommercialStatus.Available
                };
                _db.Add(unit);
                existingUnits.Add(unit);
                response.UnitsCreated++;
            }

            foreach (var row in batch.Rows)
            {
                row.Committed = row.IsValid;
            }

            batch.Status = ImportBatchStatus.Completed;
            batch.CommittedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);

            // The change tracker may still hold entities from the failed
            // attempt (new buildings/units, modified rows) — discard them so
            // recording the failure does not retry writing that same broken
            // graph on a context that already rolled back.
            foreach (var entry in _db.ChangeTracker.Entries().Where(e => e.Entity != batch).ToList())
            {
                entry.State = EntityState.Detached;
            }

            batch.Status = ImportBatchStatus.Failed;
            batch.FailureReason = "Échec de l'écriture ; aucune modification n'a été appliquée.";
            await _db.SaveChangesAsync(ct);
            throw;
        }

        response.Status = batch.Status.ToString();
        response.Message = $"{response.BuildingsCreated} bâtiment(s) créé(s), {response.UnitsCreated} bien(s) créé(s), {response.UnitsUpdated} bien(s) mis à jour.";
        return response;
    }
}
