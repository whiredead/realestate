using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Security;
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
            .Select(x => new ProjectStatusReferenceDto(x.Code, x.Label, x.SortOrder, x.IsActive))
            .ToListAsync(cancellationToken);
        return Ok(rows);
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
        return Ok(new ProjectStatusReferenceDto(status.Code, status.Label, status.SortOrder, status.IsActive));
    }
}

public sealed record ProjectStatusReferenceDto(string Code, string Label, int SortOrder, bool IsActive);
public sealed record UpdateProjectStatusReferenceRequest(string Label, int SortOrder, bool IsActive);
