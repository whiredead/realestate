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
    [HttpPost] public async Task<ActionResult<FeatureReference>> Create(FeatureReference input, CancellationToken ct) { if (string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { message = "Le nom est obligatoire." }); if (!string.IsNullOrWhiteSpace(input.IconUrl)) _media.EnsureImageUrl(input.IconUrl, "IconUrl"); input.Id = Guid.NewGuid(); input.Name = input.Name.Trim(); input.Scope = Scope(input.Scope); _db.Add(input); await _db.SaveChangesAsync(ct); return Ok(input); }
    [HttpPut("{id:guid}")] public async Task<ActionResult<FeatureReference>> Update(Guid id, FeatureReference input, CancellationToken ct) { var row = await _db.FeatureReferences.FindAsync([id], ct); if (row is null) return NotFound(); if (string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { message = "Le nom est obligatoire." }); if (!string.IsNullOrWhiteSpace(input.IconUrl)) _media.EnsureImageUrl(input.IconUrl, "IconUrl"); row.Name = input.Name.Trim(); row.Description = input.Description?.Trim(); row.IconUrl = input.IconUrl?.Trim(); row.Scope = Scope(input.Scope); row.IsActive = input.IsActive; await _db.SaveChangesAsync(ct); return Ok(row); }
    private static string Scope(string? value) => value?.Trim().ToUpperInvariant() is "PROJECT" or "QUARTIER" or "BOTH" ? value.Trim().ToUpperInvariant() : "BOTH";
}
