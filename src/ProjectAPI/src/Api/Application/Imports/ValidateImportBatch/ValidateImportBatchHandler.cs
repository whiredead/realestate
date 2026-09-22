using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Media;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Imports;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Imports.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Imports.ValidateImportBatch;

/// <summary>
/// §5.11 — dry-run: parses and validates every sheet/row, persists the batch
/// and its row-level results, but touches NO stock table.
/// CommitImportBatchHandler is the only place Buildings/Units are actually
/// written.
/// </summary>
public class ValidateImportBatchHandler : IRequestHandler<ValidateImportBatchCommand, ValidateImportBatchResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;
    private readonly MediaUrlPolicy _media;

    public ValidateImportBatchHandler(ApplicationDbContext db, ProjectScopeService projectScope, MediaUrlPolicy media)
    {
        _db = db;
        _projectScope = projectScope;
        _media = media;
    }

    public async Task<ValidateImportBatchResponse> Handle(ValidateImportBatchCommand request, CancellationToken ct)
    {
        await _projectScope.EnsureProjectAccessAsync(request.ProjectId, ct);

        var project = await _db.Set<Project>().FirstOrDefaultAsync(p => p.Id == request.ProjectId, ct)
            ?? throw new NotFoundException($"Project {request.ProjectId} not found.");

        if (request.FileContent.Length == 0)
        {
            throw new BusinessRuleException(BusinessErrorCodes.ValidationFailed, "Le fichier est vide.");
        }

        var fileHash = Convert.ToHexString(SHA256.HashData(request.FileContent));

        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            ProjectName = project.Name,
            FileName = request.FileName,
            FileHash = fileHash,
            Status = ImportBatchStatus.Validating,
            CreatedBy = request.CreatedBy,
            CreatedAt = DateTime.UtcNow
        };

        ImportWorkbookReader.ParsedWorkbook parsed;
        try
        {
            parsed = ImportWorkbookReader.Parse(request.FileContent);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or ArgumentException or NotSupportedException)
        {
            // ClosedXML throws a mix of these for "this isn't a valid .xlsx" —
            // a corrupt upload or the wrong file type must read as a clean,
            // actionable failed batch, not a bare 500.
            return await FailBatch(batch, $"Fichier illisible : le classeur est corrompu ou n'est pas un fichier .xlsx valide ({ex.Message}).", ct);
        }

        if (parsed.StructuralError is not null)
        {
            return await FailBatch(batch, parsed.StructuralError, ct);
        }

        // Cross-tab check: every unit's BuildingName must resolve to a
        // building SHEET in THIS file (existing buildings are matched by name
        // at commit time; here we only confirm internal consistency). Excel
        // itself refuses two sheets with the same name, so no duplicate check
        // is needed here.
        var buildingNames = parsed.Buildings
            .Where(b => b.IsValid)
            .Select(b => b.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingBuildingNames = await _db.Set<Immeuble>()
            .Where(im => im.ProjectId == request.ProjectId)
            .Select(im => im.Name)
            .ToListAsync(ct);
        var knownNames = new HashSet<string>(buildingNames, StringComparer.OrdinalIgnoreCase);
        knownNames.UnionWith(existingBuildingNames);

        // Only the types already attached to THIS project — the same set
        // CreateUnit/UpdateUnit offer in the UI, so an import can never
        // assign a type the project's own forms wouldn't let you pick.
        var projectTypeBienNames = await _db.Set<Project>()
            .Where(p => p.Id == request.ProjectId)
            .SelectMany(p => p.TypeBiens.Where(link => link.TypeBien != null).Select(link => link.TypeBien!.Name))
            .ToListAsync(ct);
        var knownTypeBienNames = new HashSet<string>(projectTypeBienNames, StringComparer.OrdinalIgnoreCase);

        var rows = new List<ImportRow>();
        var errorDtos = new List<ValidateImportBatchResponse.RowErrorDto>();

        foreach (var b in parsed.Buildings)
        {
            var buildingErrors = new List<string>();
            if (!b.IsValid)
            {
                buildingErrors.Add(b.Error!);
            }
            else
            {
                // Same allowlist CreateImmeubleCommand's Module3DLink goes
                // through — a bulk import must not be a back door around the
                // host restriction every other path enforces.
                try
                {
                    _media.Ensure3DLink(b.Module3DLink, "Module3DLink");
                }
                catch (BusinessRuleException ex)
                {
                    buildingErrors.Add(ex.Message);
                }
            }

            var isValid = buildingErrors.Count == 0;
            rows.Add(new ImportRow
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                Sheet = b.Name,
                RowNumber = b.RowNumber,
                RawData = b.RawJson,
                IsValid = isValid,
                Errors = isValid ? null : string.Join(" ", buildingErrors)
            });
            if (!isValid)
            {
                errorDtos.Add(new ValidateImportBatchResponse.RowErrorDto { Sheet = b.Name, RowNumber = b.RowNumber, Message = string.Join(" ", buildingErrors) });
            }
        }

        foreach (var u in parsed.Units)
        {
            var unitErrors = new List<string>();
            if (!u.IsValid)
            {
                unitErrors.Add(u.Error!);
            }
            else if (!knownNames.Contains(u.BuildingName))
            {
                // Should not happen in practice — the sheet that produced this
                // unit row IS the building — but a building row that failed
                // its own validation (e.g. an empty tab name is impossible,
                // yet defensively covered) would otherwise silently orphan
                // its units.
                unitErrors.Add($"Bâtiment '{u.BuildingName}' invalide — voir l'erreur sur cet onglet.");
            }

            // Blank is fine — CommitImportBatchHandler then matches on bedroom
            // count, same as every other unit-creation path. A NAMED type that
            // doesn't exist on this project is the only thing refused here.
            if (u.IsValid && !string.IsNullOrWhiteSpace(u.TypeBien) && !knownTypeBienNames.Contains(u.TypeBien))
            {
                unitErrors.Add($"Type de bien '{u.TypeBien}' introuvable pour ce projet.");
            }

            var isValid = unitErrors.Count == 0;
            rows.Add(new ImportRow
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                Sheet = u.BuildingName,
                RowNumber = u.RowNumber,
                RawData = u.RawJson,
                IsValid = isValid,
                Errors = isValid ? null : string.Join(" ", unitErrors)
            });
            if (!isValid)
            {
                errorDtos.Add(new ValidateImportBatchResponse.RowErrorDto { Sheet = u.BuildingName, RowNumber = u.RowNumber, Message = string.Join(" ", unitErrors) });
            }
        }

        batch.TotalRows = rows.Count;
        batch.ErrorRowCount = rows.Count(r => !r.IsValid);
        batch.Status = batch.ErrorRowCount > 0 ? ImportBatchStatus.ValidationFailed : ImportBatchStatus.ReadyToCommit;
        batch.ValidatedAt = DateTime.UtcNow;

        _db.Add(batch);
        _db.AddRange(rows);
        await _db.SaveChangesAsync(ct);

        return new ValidateImportBatchResponse
        {
            BatchId = batch.Id,
            Status = batch.Status.ToString(),
            TotalRows = batch.TotalRows,
            ErrorRowCount = batch.ErrorRowCount,
            Errors = errorDtos
        };
    }

    private async Task<ValidateImportBatchResponse> FailBatch(ImportBatch batch, string reason, CancellationToken ct)
    {
        batch.Status = ImportBatchStatus.ValidationFailed;
        batch.FailureReason = reason;
        _db.Add(batch);
        await _db.SaveChangesAsync(ct);

        return new ValidateImportBatchResponse
        {
            BatchId = batch.Id,
            Status = batch.Status.ToString(),
            Errors = { new ValidateImportBatchResponse.RowErrorDto { Sheet = "-", RowNumber = 0, Message = reason } }
        };
    }
}
