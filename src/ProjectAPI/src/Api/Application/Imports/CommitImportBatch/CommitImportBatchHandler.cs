using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Imports;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Imports.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;
using ImmeubleEntity = ProjectAPI.Domain.Immeubles.Entities.Immeuble;
using UnitEntity = ProjectAPI.Domain.Immeubles.Entities.Unit;
using ValidationException = ProjectAPI.Api.Application.Common.Exceptions.ValidationException;

namespace ProjectAPI.Api.Application.Imports.CommitImportBatch;

/// <summary>
/// §5.11/§23 — the only place Buildings/Units are actually written from an
/// import, into the project chosen up front (validated against). All-or-
/// nothing: one DB transaction wraps every write this handler makes, so if
/// any row fails partway through, everything already written in THIS
/// commit — buildings, floors, units — is rolled back together. Nothing
/// partial is ever left behind; the admin sees one failed batch and can fix
/// the file and retry from scratch. A fingerprint mismatch (file hash, row
/// count) throws IMPORT_SOURCE_CHANGED before touching anything, per "commit
/// refused if file/project/rows changed since validation".
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

        if (!batch.ProjectId.HasValue)
        {
            // Can only happen for a structurally-failed batch that was never
            // scoped to a project — ReadyToCommit is unreachable without one,
            // so the status check below would already refuse it, but guard
            // explicitly rather than dereferencing a null Guid.
            throw BusinessRuleException.ImportSourceChanged();
        }

        await _projectScope.EnsureProjectAccessAsync(batch.ProjectId.Value, ct);

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

        ImportWorkbookReader.ParsedWorkbook parsed;
        try
        {
            parsed = ImportWorkbookReader.Parse(request.FileContent);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or ArgumentException or NotSupportedException)
        {
            // The file passed validation but is unreadable now (re-saved,
            // truncated, swapped) — refuse cleanly rather than a bare 500.
            throw BusinessRuleException.ImportSourceChanged();
        }

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
            var project = await _db.Set<Project>().FirstOrDefaultAsync(p => p.Id == batch.ProjectId!.Value, ct)
                ?? throw BusinessRuleException.ImportSourceChanged(); // validated against a project that no longer exists

            var existingBuildings = await _db.Set<ImmeubleEntity>()
                .Where(im => im.ProjectId == project.Id)
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
                    ProjectId = project.Id,
                    Name = b.Name,
                    Location = b.Location,
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

            // Floor rows are resolved/created per building — name-matched,
            // never duplicated across re-imports — from whatever label each
            // unit's Floor column carries. There is no separate Floors sheet:
            // one sheet per building is already the organising unit; a floor
            // is just a grouping within it.
            var existingFloors = await _db.Set<Floor>()
                .Where(f => existingBuildings.Values.Select(im => im.Id).Contains(f.ImmeubleId))
                .ToListAsync(ct);

            // Same set ValidateImportBatchHandler checked names against — a
            // blank TypeBien column falls back to null here, same as every
            // other unit-creation path (matched on bedroom count instead).
            var projectTypeBienRows = await _db.Set<Project>()
                .Where(p => p.Id == project.Id)
                .SelectMany(p => p.TypeBiens.Where(link => link.TypeBien != null).Select(link => link.TypeBien!))
                .ToListAsync(ct);
            var projectTypeBiens = projectTypeBienRows
                .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

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
                    response.FloorsCreated++;
                }

                // Blank TypeBien -> null: falls back to the bedroom-count match
                // ValidateImportBatchHandler already confirmed is safe (blank
                // rows never produced a row error).
                int? typeBienId = u.TypeBien is not null && projectTypeBiens.TryGetValue(u.TypeBien, out var matchedType)
                    ? matchedType.Id
                    : null;

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
                    existingUnit.TotalSurface = u.TotalSurface;
                    // View/Orientation are NOT NULL columns; the sheet's are
                    // optional, so a blank cell must not become a literal
                    // NULL write (that fails the insert/update outright).
                    existingUnit.View = u.View ?? string.Empty;
                    existingUnit.Orientation = u.Orientation ?? string.Empty;
                    if (typeBienId.HasValue) existingUnit.TypeBienId = typeBienId;
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
                    TotalSurface = u.TotalSurface,
                    View = u.View ?? string.Empty,
                    Orientation = u.Orientation ?? string.Empty,
                    TypeBienId = typeBienId,
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
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);

            // The change tracker may still hold entities from the failed
            // attempt (new buildings/floors/units, modified rows). Detaching
            // them one by one lets EF's navigation fixup try to null out
            // required FKs on entities still left tracked (a Unit's FloorId,
            // say) as each parent is detached out from under it — which
            // itself throws. Clear() drops every tracked entity in one step,
            // with no per-entity fixup, so it can never fail this way; batch
            // is then re-attached to record the failure.
            _db.ChangeTracker.Clear();
            _db.Attach(batch);

            // Known, user-safe exceptions (a business rule, a stale
            // reference) keep their own message on the batch's audit trail;
            // anything else is recorded generically so no internal detail
            // (SQL, stack trace) ends up stored or ever echoed back.
            batch.FailureReason = ex is BusinessRuleException or NotFoundException or ValidationException
                ? ex.Message
                : "Échec de l'écriture ; aucune modification n'a été appliquée.";
            batch.Status = ImportBatchStatus.Failed;
            await _db.SaveChangesAsync(ct);
            throw;
        }

        response.Status = batch.Status.ToString();
        response.Message = $"{response.BuildingsCreated} bâtiment(s), {response.FloorsCreated} étage(s) créé(s), {response.UnitsCreated} bien(s) créé(s), {response.UnitsUpdated} bien(s) mis à jour.";
        return response;
    }
}
