using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Imports;
using ProjectAPI.Domain.Imports.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Imports.ValidateImportBatch;

/// <summary>
/// §5.11 — dry-run: parses and validates every row, persists the batch and
/// its row-level results, but touches NO stock table. CommitImportBatchHandler
/// is the only place Buildings/Units are actually written.
/// </summary>
public class ValidateImportBatchHandler : IRequestHandler<ValidateImportBatchCommand, ValidateImportBatchResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public ValidateImportBatchHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<ValidateImportBatchResponse> Handle(ValidateImportBatchCommand request, CancellationToken ct)
    {
        await _projectScope.EnsureProjectAccessAsync(request.ProjectId, ct);

        _ = await _db.Set<Project>().FirstOrDefaultAsync(p => p.Id == request.ProjectId, ct)
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
        catch (Exception ex)
        {
            batch.Status = ImportBatchStatus.Failed;
            batch.FailureReason = $"Fichier illisible : {ex.Message}";
            _db.Add(batch);
            await _db.SaveChangesAsync(ct);

            return new ValidateImportBatchResponse
            {
                BatchId = batch.Id,
                Status = batch.Status.ToString(),
                Errors = { new ValidateImportBatchResponse.RowErrorDto { Sheet = "-", RowNumber = 0, Message = batch.FailureReason } }
            };
        }

        if (parsed.StructuralError is not null)
        {
            batch.Status = ImportBatchStatus.ValidationFailed;
            batch.FailureReason = parsed.StructuralError;
            _db.Add(batch);
            await _db.SaveChangesAsync(ct);

            return new ValidateImportBatchResponse
            {
                BatchId = batch.Id,
                Status = batch.Status.ToString(),
                Errors = { new ValidateImportBatchResponse.RowErrorDto { Sheet = "-", RowNumber = 0, Message = parsed.StructuralError } }
            };
        }

        // Cross-tab check: every unit's BuildingName must resolve to a
        // building row in THIS file (existing buildings are matched by name
        // at commit time; here we only confirm internal consistency).
        var buildingNames = parsed.Buildings
            .Where(b => b.IsValid)
            .Select(b => b.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingBuildingNames = await _db.Set<Domain.Immeubles.Entities.Immeuble>()
            .Where(im => im.ProjectId == request.ProjectId)
            .Select(im => im.Name)
            .ToListAsync(ct);
        var knownNames = new HashSet<string>(buildingNames, StringComparer.OrdinalIgnoreCase);
        knownNames.UnionWith(existingBuildingNames);

        var rows = new List<ImportRow>();
        var errorDtos = new List<ValidateImportBatchResponse.RowErrorDto>();

        foreach (var b in parsed.Buildings)
        {
            rows.Add(new ImportRow
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                Sheet = "Buildings",
                RowNumber = b.RowNumber,
                RawData = b.RawJson,
                IsValid = b.IsValid,
                Errors = b.Error
            });
            if (!b.IsValid)
            {
                errorDtos.Add(new ValidateImportBatchResponse.RowErrorDto { Sheet = "Buildings", RowNumber = b.RowNumber, Message = b.Error! });
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
                unitErrors.Add($"Bâtiment '{u.BuildingName}' introuvable (ni dans ce fichier, ni existant).");
            }

            var isValid = unitErrors.Count == 0;
            rows.Add(new ImportRow
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                Sheet = "Units",
                RowNumber = u.RowNumber,
                RawData = u.RawJson,
                IsValid = isValid,
                Errors = isValid ? null : string.Join(" ", unitErrors)
            });
            if (!isValid)
            {
                errorDtos.Add(new ValidateImportBatchResponse.RowErrorDto { Sheet = "Units", RowNumber = u.RowNumber, Message = string.Join(" ", unitErrors) });
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
}
