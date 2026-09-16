using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;
namespace ProjectAPI.Api.Controllers;
[ApiController, Route("api/reference-items"), Authorize(Roles = RoleGroups.Admins)]
public sealed class ReferenceItemsController : ControllerBase
{
 private readonly ApplicationDbContext _db; public ReferenceItemsController(ApplicationDbContext db) => _db = db;
 [HttpGet] public Task<List<ReferenceItem>> List([FromQuery] string? category, CancellationToken ct) => _db.ReferenceItems.AsNoTracking().Where(x => category == null || x.Category == category).OrderBy(x => x.Category).ThenBy(x => x.SortOrder).ThenBy(x => x.Label).ToListAsync(ct);
 [HttpPost] public async Task<ActionResult<ReferenceItem>> Create(ReferenceItem item, CancellationToken ct) { if (string.IsNullOrWhiteSpace(item.Category) || string.IsNullOrWhiteSpace(item.Code) || string.IsNullOrWhiteSpace(item.Label)) return BadRequest(new { message = "Catégorie, code et libellé sont obligatoires." }); item.Id = Guid.NewGuid(); item.Category = item.Category.Trim().ToUpperInvariant(); item.Code = item.Code.Trim().ToUpperInvariant(); item.Label = item.Label.Trim(); _db.Add(item); await _db.SaveChangesAsync(ct); return Ok(item); }
 [HttpPut("{id:guid}")] public async Task<ActionResult<ReferenceItem>> Update(Guid id, ReferenceItem input, CancellationToken ct) { var item = await _db.ReferenceItems.FindAsync([id], ct); if (item is null) return NotFound(); if (string.IsNullOrWhiteSpace(input.Label)) return BadRequest(new { message = "Le libellé est obligatoire." }); item.Label=input.Label.Trim(); item.Description=input.Description?.Trim(); item.SortOrder=input.SortOrder; item.IsActive=input.IsActive; await _db.SaveChangesAsync(ct); return Ok(item); }
 [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { var item=await _db.ReferenceItems.FindAsync([id],ct); if(item is null) return NotFound(); _db.Remove(item); await _db.SaveChangesAsync(ct); return NoContent(); }
}
