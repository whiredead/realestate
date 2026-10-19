using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Controllers;

/// <summary>
/// Status referential: how each existing status is PRESENTED (label, note, badge tone).
///
/// Statuses themselves — their codes and the workflow rules attached to them —
/// are defined by the code and cannot be created or deleted here. A row stored
/// here only overrides the wording/colour of one (family, code) pair; deleting
/// it restores the default. Nothing in the backend reads these rows: they are
/// presentation data, which is why reading them is open to every caller.
/// </summary>
[ApiController, Route("api/status-references")]
public sealed partial class StatusReferencesController : ControllerBase
{
    public const string CategoryPrefix = "STATUS.";
    private static readonly string[] Tones = { "success", "warning", "info", "danger", "critical", "neutral" };

    private readonly ApplicationDbContext _db;
    public StatusReferencesController(ApplicationDbContext db) => _db = db;

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _db.ReferenceItems.AsNoTracking()
            .Where(x => x.Category.StartsWith(CategoryPrefix))
            .Select(x => new StatusReferenceDto(x.Category.Substring(CategoryPrefix.Length), x.Code, x.Label, x.Description, x.Tone))
            .ToListAsync(ct));

    /// <summary>Overrides the presentation of one existing status.</summary>
    [HttpPut("{family}/{code}"), Authorize(Roles = RoleGroups.GlobalOnly)]
    public async Task<IActionResult> Upsert(string family, string code, [FromBody] StatusReferenceInput input, CancellationToken ct)
    {
        if (!Key().IsMatch(family) || !Key().IsMatch(code))
            return BadRequest(new { message = "Famille ou code de statut invalide." });
        if (string.IsNullOrWhiteSpace(input.Label))
            return BadRequest(new { message = "Le libellé est obligatoire." });
        if (input.Tone is not null && !Tones.Contains(input.Tone))
            return BadRequest(new { message = $"Couleur inconnue. Valeurs : {string.Join(", ", Tones)}." });

        var category = CategoryPrefix + family.ToUpperInvariant();
        var row = await _db.ReferenceItems.FirstOrDefaultAsync(x => x.Category == category && x.Code == code, ct);
        if (row is null)
        {
            row = new ReferenceItem { Id = Guid.NewGuid(), Category = category, Code = code, IsActive = true };
            _db.Add(row);
        }
        row.Label = input.Label.Trim();
        row.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        row.Tone = input.Tone;
        await _db.SaveChangesAsync(ct);
        return Ok(new StatusReferenceDto(family.ToUpperInvariant(), row.Code, row.Label, row.Description, row.Tone));
    }

    /// <summary>Drops the override: the status shows its default label and colour again.</summary>
    [HttpDelete("{family}/{code}"), Authorize(Roles = RoleGroups.GlobalOnly)]
    public async Task<IActionResult> Reset(string family, string code, CancellationToken ct)
    {
        var category = CategoryPrefix + family.ToUpperInvariant();
        var row = await _db.ReferenceItems.FirstOrDefaultAsync(x => x.Category == category && x.Code == code, ct);
        if (row is not null)
        {
            _db.Remove(row);
            await _db.SaveChangesAsync(ct);
        }
        return NoContent();
    }

    // Codes keep their wire casing (e.g. "ReadyForNotary" or "SUBMITTED").
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,60}$")]
    private static partial Regex Key();
}

public sealed record StatusReferenceDto(string Family, string Code, string Label, string? Description, string? Tone);
public sealed record StatusReferenceInput(string Label, string? Description, string? Tone);
