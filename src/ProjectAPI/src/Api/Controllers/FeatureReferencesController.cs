using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Media;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Controllers;

[ApiController]
[Route("api/feature-references")]
[Authorize(Roles = RoleGroups.Admins)]
public sealed class FeatureReferencesController : ControllerBase
{
    private readonly ApplicationDbContext _db; private readonly MediaUrlPolicy _media;
    public FeatureReferencesController(ApplicationDbContext db, MediaUrlPolicy media) { _db = db; _media = media; }
    [HttpGet] public Task<List<FeatureReference>> List(CancellationToken ct) => _db.FeatureReferences.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
    [HttpPost] public async Task<ActionResult<FeatureReference>> Create(FeatureReference input, CancellationToken ct) { Validate(input); if (!string.IsNullOrWhiteSpace(input.IconUrl)) _media.EnsureImageUrl(input.IconUrl, "IconUrl"); input.Id = Guid.NewGuid(); input.Name = input.Name.Trim(); input.Scope = Scope(input.Scope); _db.Add(input); await _db.SaveChangesAsync(ct); return Ok(input); }
    [HttpPut("{id:guid}")] public async Task<ActionResult<FeatureReference>> Update(Guid id, FeatureReference input, CancellationToken ct) { var row = await _db.FeatureReferences.FindAsync([id], ct); if (row is null) return NotFound(); Validate(input); if (!string.IsNullOrWhiteSpace(input.IconUrl)) _media.EnsureImageUrl(input.IconUrl, "IconUrl"); row.Name = input.Name.Trim(); row.Description = input.Description?.Trim(); row.IconUrl = input.IconUrl?.Trim(); row.Scope = Scope(input.Scope); row.IsActive = input.IsActive; await _db.SaveChangesAsync(ct); return Ok(row); }
    /// <summary>Field errors as a structured 422 (RFC 9457) instead of an ad-hoc {message} body or a 500 on an over-long value.</summary>
    private static void Validate(FeatureReference input)
    {
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        if (string.IsNullOrWhiteSpace(input.Name)) failures.Add(new("Name", "Le nom est obligatoire."));
        else if (input.Name.Trim().Length > 150) failures.Add(new("Name", "Le nom ne doit pas dépasser 150 caractères."));
        if (input.Description?.Trim().Length > 2000) failures.Add(new("Description", "La description ne doit pas dépasser 2000 caractères."));
        if (failures.Count > 0) throw new Application.Common.Exceptions.ValidationException(failures);
    }
    private static string Scope(string? value) => value?.Trim().ToUpperInvariant() is "PROJECT" or "QUARTIER" or "BOTH" ? value.Trim().ToUpperInvariant() : "BOTH";
}
