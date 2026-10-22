using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.References.ProjectStatusReferences;

public sealed record ProjectStatusReferenceDto(string Code, string Label, string BusinessPhase, int SortOrder, bool IsActive)
{
    public static ProjectStatusReferenceDto From(ProjectStatusReference s) => new(s.Code, s.Label, s.BusinessPhase, s.SortOrder, s.IsActive);
}

internal static class ProjectStatusReferenceRules
{
    public static string ValidLabel(string? value, int sortOrder)
    {
        var label = value?.Trim();
        if (string.IsNullOrWhiteSpace(label) || label.Length > 100)
            throw new Common.Exceptions.ValidationException([new ValidationFailure("Label", "Le libellé est obligatoire et limité à 100 caractères.")]);
        if (sortOrder < 0)
            throw new Common.Exceptions.ValidationException([new ValidationFailure("SortOrder", "L'ordre doit être positif.")]);
        return label;
    }

    public static string ToCode(string label)
    {
        var decomposed = label.Normalize(NormalizationForm.FormD);
        var withoutAccents = new string(decomposed.Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray());
        var code = Regex.Replace(withoutAccents.ToUpperInvariant(), "[^A-Z0-9]+", "_").Trim('_');
        return string.IsNullOrEmpty(code) ? "STATUT" : code[..Math.Min(code.Length, 50)];
    }
}

// GET /api/project-status-references — project creation reads its status choices from this table.
public class GetProjectStatusReferencesQuery : IRequest<List<ProjectStatusReferenceDto>> { }

public class GetProjectStatusReferencesHandler : IRequestHandler<GetProjectStatusReferencesQuery, List<ProjectStatusReferenceDto>>
{
    private readonly ApplicationDbContext _db;
    public GetProjectStatusReferencesHandler(ApplicationDbContext db) => _db = db;

    public Task<List<ProjectStatusReferenceDto>> Handle(GetProjectStatusReferencesQuery request, CancellationToken ct) =>
        _db.ProjectStatusReferences.AsNoTracking()
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Code)
            .Select(x => new ProjectStatusReferenceDto(x.Code, x.Label, x.BusinessPhase, x.SortOrder, x.IsActive))
            .ToListAsync(ct);
}

// POST /api/project-status-references
public class CreateProjectStatusReferenceCommand : IRequest<ProjectStatusReferenceDto>
{
    public string? Label { get; set; }
    public string? BusinessPhase { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public class CreateProjectStatusReferenceHandler : IRequestHandler<CreateProjectStatusReferenceCommand, ProjectStatusReferenceDto>
{
    private readonly ApplicationDbContext _db;
    public CreateProjectStatusReferenceHandler(ApplicationDbContext db) => _db = db;

    public async Task<ProjectStatusReferenceDto> Handle(CreateProjectStatusReferenceCommand request, CancellationToken ct)
    {
        var businessPhase = ProjectStatusCodes.Normalize(request.BusinessPhase);
        var label = ProjectStatusReferenceRules.ValidLabel(request.Label, request.SortOrder);

        var baseCode = ProjectStatusReferenceRules.ToCode(label);
        var code = baseCode;
        var suffix = 2;
        while (await _db.ProjectStatusReferences.AnyAsync(x => x.Code == code, ct))
        {
            var ending = $"_{suffix++}";
            code = $"{baseCode[..Math.Min(baseCode.Length, 50 - ending.Length)]}{ending}";
        }

        var status = new ProjectStatusReference { Code = code, Label = label, BusinessPhase = businessPhase, SortOrder = request.SortOrder, IsActive = request.IsActive };
        _db.ProjectStatusReferences.Add(status);
        await _db.SaveChangesAsync(ct);
        return ProjectStatusReferenceDto.From(status);
    }
}

// PUT /api/project-status-references/{code} — presentation and availability only; lifecycle codes are immutable.
public class UpdateProjectStatusReferenceCommand : IRequest<ProjectStatusReferenceDto>
{
    public string Code { get; set; } = string.Empty;
    public string? Label { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public class UpdateProjectStatusReferenceHandler : IRequestHandler<UpdateProjectStatusReferenceCommand, ProjectStatusReferenceDto>
{
    private readonly ApplicationDbContext _db;
    public UpdateProjectStatusReferenceHandler(ApplicationDbContext db) => _db = db;

    public async Task<ProjectStatusReferenceDto> Handle(UpdateProjectStatusReferenceCommand request, CancellationToken ct)
    {
        var status = await _db.ProjectStatusReferences.FindAsync([request.Code], ct)
            ?? throw new NotFoundException(nameof(ProjectStatusReference), request.Code);
        var label = ProjectStatusReferenceRules.ValidLabel(request.Label, request.SortOrder);

        status.Label = label;
        status.SortOrder = request.SortOrder;
        status.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        return ProjectStatusReferenceDto.From(status);
    }
}
