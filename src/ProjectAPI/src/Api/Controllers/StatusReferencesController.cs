using Microsoft.AspNetCore.Authorization;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.References.StatusReferences;

namespace ProjectAPI.Api.Controllers;

/// <summary>
/// Status referential: how each existing status is PRESENTED (label, note, badge tone).
/// See <see cref="StatusReferenceRules"/>; reading is open to every caller because it is presentation data.
/// </summary>
[ApiController, Route("api/status-references")]
public sealed class StatusReferencesController : ControllerBase
{
    private readonly IMediator _mediator;
    public StatusReferencesController(IMediator mediator) => _mediator = mediator;

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetStatusReferencesQuery(), ct));

    /// <summary>Overrides the presentation of one existing status.</summary>
    [HttpPut("{family}/{code}"), Authorize(Roles = RoleGroups.GlobalOnly)]
    public async Task<IActionResult> Upsert(string family, string code, [FromBody] StatusReferenceInput input, CancellationToken ct) =>
        Ok(await _mediator.Send(new UpsertStatusReferenceCommand
        {
            Family = family,
            Code = code,
            Label = input.Label,
            Description = input.Description,
            Tone = input.Tone
        }, ct));

    /// <summary>Drops the override: the status shows its default label and colour again.</summary>
    [HttpDelete("{family}/{code}"), Authorize(Roles = RoleGroups.GlobalOnly)]
    public async Task<IActionResult> Reset(string family, string code, CancellationToken ct)
    {
        await _mediator.Send(new ResetStatusReferenceCommand { Family = family, Code = code }, ct);
        return NoContent();
    }
}

public sealed record StatusReferenceInput(string Label, string? Description, string? Tone);
