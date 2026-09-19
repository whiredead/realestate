using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Controllers;

[ApiController]
[Route("api/project-status-references")]
public sealed class ProjectStatusReferencesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ProjectStatusReferencesController(ApplicationDbContext db) => _db = db;

    /// <summary>Project creation reads its status choices from this reference table.</summary>
    [HttpGet]
    [Authorize(Roles = RoleGroups.Admins)]
    public async Task<ActionResult<IReadOnlyList<ProjectStatusReferenceDto>>> List(CancellationToken cancellationToken)
    {
        var rows = await _db.ProjectStatusReferences.AsNoTracking()
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Code)
            .Select(x => new ProjectStatusReferenceDto(x.Code, x.Label, x.BusinessPhase, x.SortOrder, x.IsActive))
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }

    [HttpPost]
    [Authorize(Roles = RoleGroups.GlobalOnly)]
    public async Task<ActionResult<ProjectStatusReferenceDto>> Create(
        [FromBody] CreateProjectStatusReferenceRequest request,
        CancellationToken cancellationToken)
    {
        var label = request.Label?.Trim();
        var businessPhase = ProjectStatusCodes.Normalize(request.BusinessPhase);
        if (string.IsNullOrWhiteSpace(label) || label.Length > 100)
            return BadRequest(new { message = "Le libellé est obligatoire et limité à 100 caractères." });
        if (request.SortOrder < 0)
            return BadRequest(new { message = "L'ordre doit être positif." });

        var baseCode = ToCode(label);
        var code = baseCode;
        var suffix = 2;
        while (await _db.ProjectStatusReferences.AnyAsync(x => x.Code == code, cancellationToken))
        {
            var ending = $"_{suffix++}";
            code = $"{baseCode[..Math.Min(baseCode.Length, 50 - ending.Length)]}{ending}";
        }

        var status = new ProjectStatusReference { Code = code, Label = label, BusinessPhase = businessPhase, SortOrder = request.SortOrder, IsActive = request.IsActive };
        _db.ProjectStatusReferences.Add(status);
        await _db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(List), new ProjectStatusReferenceDto(status.Code, status.Label, status.BusinessPhase, status.SortOrder, status.IsActive));
    }

    /// <summary>Updates presentation and availability only; lifecycle codes are intentionally immutable.</summary>
    [HttpPut("{code}")]
    [Authorize(Roles = RoleGroups.Admins)]
    public async Task<ActionResult<ProjectStatusReferenceDto>> Update(
        string code,
        [FromBody] UpdateProjectStatusReferenceRequest request,
        CancellationToken cancellationToken)
    {
        var status = await _db.ProjectStatusReferences.FindAsync([code], cancellationToken);
        if (status is null) return NotFound();

        var label = request.Label?.Trim();
        if (string.IsNullOrWhiteSpace(label) || label.Length > 100)
            return BadRequest(new { message = "Le libellé est obligatoire et limité à 100 caractères." });
        if (request.SortOrder < 0)
            return BadRequest(new { message = "L'ordre doit être positif." });

        status.Label = label;
        status.SortOrder = request.SortOrder;
        status.IsActive = request.IsActive;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new ProjectStatusReferenceDto(status.Code, status.Label, status.BusinessPhase, status.SortOrder, status.IsActive));
    }

    private static string ToCode(string label)
    {
        var decomposed = label.Normalize(NormalizationForm.FormD);
        var withoutAccents = new string(decomposed.Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark).ToArray());
        var code = Regex.Replace(withoutAccents.ToUpperInvariant(), "[^A-Z0-9]+", "_").Trim('_');
        return string.IsNullOrEmpty(code) ? "STATUT" : code[..Math.Min(code.Length, 50)];
    }
}

public sealed record ProjectStatusReferenceDto(string Code, string Label, string BusinessPhase, int SortOrder, bool IsActive);
public sealed record CreateProjectStatusReferenceRequest(string Label, string BusinessPhase, int SortOrder, bool IsActive);
public sealed record UpdateProjectStatusReferenceRequest(string Label, int SortOrder, bool IsActive);
