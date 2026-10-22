using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.References.StatusReferences;

/// <summary>
/// Status referential: how each existing status is PRESENTED (label, note, badge tone).
///
/// Statuses themselves — their codes and the workflow rules attached to them —
/// are defined by the code and cannot be created or deleted here. A row stored
/// here only overrides the wording/colour of one (family, code) pair; deleting
/// it restores the default. Nothing in the backend reads these rows: they are
/// presentation data, which is why reading them is open to every caller.
/// </summary>
public static partial class StatusReferenceRules
{
    public const string CategoryPrefix = "STATUS.";
    public static readonly string[] Tones = { "success", "warning", "info", "danger", "critical", "neutral" };

    // Codes keep their wire casing (e.g. "ReadyForNotary" or "SUBMITTED").
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,60}$")]
    public static partial Regex Key();
}

public sealed record StatusReferenceDto(string Family, string Code, string Label, string? Description, string? Tone);

// GET /api/status-references
public class GetStatusReferencesQuery : IRequest<List<StatusReferenceDto>> { }

public class GetStatusReferencesHandler : IRequestHandler<GetStatusReferencesQuery, List<StatusReferenceDto>>
{
    private readonly ApplicationDbContext _db;
    public GetStatusReferencesHandler(ApplicationDbContext db) => _db = db;

    public Task<List<StatusReferenceDto>> Handle(GetStatusReferencesQuery request, CancellationToken ct) =>
        _db.ReferenceItems.AsNoTracking()
            .Where(x => x.Category.StartsWith(StatusReferenceRules.CategoryPrefix))
            .Select(x => new StatusReferenceDto(x.Category.Substring(StatusReferenceRules.CategoryPrefix.Length), x.Code, x.Label, x.Description, x.Tone))
            .ToListAsync(ct);
}

// PUT /api/status-references/{family}/{code} — overrides the presentation of one existing status.
public class UpsertStatusReferenceCommand : IRequest<StatusReferenceDto>
{
    public string Family { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? Description { get; set; }
    public string? Tone { get; set; }
}

public class UpsertStatusReferenceHandler : IRequestHandler<UpsertStatusReferenceCommand, StatusReferenceDto>
{
    private readonly ApplicationDbContext _db;
    public UpsertStatusReferenceHandler(ApplicationDbContext db) => _db = db;

    public async Task<StatusReferenceDto> Handle(UpsertStatusReferenceCommand request, CancellationToken ct)
    {
        if (!StatusReferenceRules.Key().IsMatch(request.Family) || !StatusReferenceRules.Key().IsMatch(request.Code))
            throw new Common.Exceptions.ValidationException([new ValidationFailure("Code", "Famille ou code de statut invalide.")]);
        if (string.IsNullOrWhiteSpace(request.Label))
            throw new Common.Exceptions.ValidationException([new ValidationFailure("Label", "Le libellé est obligatoire.")]);
        if (request.Tone is not null && !StatusReferenceRules.Tones.Contains(request.Tone))
            throw new Common.Exceptions.ValidationException([new ValidationFailure("Tone", $"Couleur inconnue. Valeurs : {string.Join(", ", StatusReferenceRules.Tones)}.")]);

        var family = request.Family.ToUpperInvariant();
        var category = StatusReferenceRules.CategoryPrefix + family;
        var row = await _db.ReferenceItems.FirstOrDefaultAsync(x => x.Category == category && x.Code == request.Code, ct);
        if (row is null)
        {
            row = new ReferenceItem { Id = Guid.NewGuid(), Category = category, Code = request.Code, IsActive = true };
            _db.Add(row);
        }
        row.Label = request.Label.Trim();
        row.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        row.Tone = request.Tone;
        await _db.SaveChangesAsync(ct);
        return new StatusReferenceDto(family, row.Code, row.Label, row.Description, row.Tone);
    }
}

// DELETE /api/status-references/{family}/{code} — drops the override: the status shows its default label and colour again.
public class ResetStatusReferenceCommand : IRequest<MediatR.Unit>
{
    public string Family { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class ResetStatusReferenceHandler : IRequestHandler<ResetStatusReferenceCommand, MediatR.Unit>
{
    private readonly ApplicationDbContext _db;
    public ResetStatusReferenceHandler(ApplicationDbContext db) => _db = db;

    public async Task<MediatR.Unit> Handle(ResetStatusReferenceCommand request, CancellationToken ct)
    {
        var category = StatusReferenceRules.CategoryPrefix + request.Family.ToUpperInvariant();
        var row = await _db.ReferenceItems.FirstOrDefaultAsync(x => x.Category == category && x.Code == request.Code, ct);
        if (row is not null)
        {
            _db.Remove(row);
            await _db.SaveChangesAsync(ct);
        }
        return MediatR.Unit.Value;
    }
}
