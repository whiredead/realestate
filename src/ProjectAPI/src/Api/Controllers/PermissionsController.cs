using Microsoft.AspNetCore.Authorization;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Identity.Permissions;

namespace ProjectAPI.Api.Controllers;

/// <summary>
/// Endpoint permission catalogue and its role/user overrides. Action names are part of the
/// catalogue keys ("permissions.setuser.put"…): renaming one orphans the rules stored for it.
/// </summary>
[ApiController, Route("api/permissions"), Authorize(Roles = RoleGroups.Admins)]
public sealed class PermissionsController : ControllerBase
{
    private readonly IMediator _mediator;
    public PermissionsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetPermissionCatalogQuery(), ct));

    [HttpPost("catalog/sync")]
    public async Task<IActionResult> SyncCatalog(CancellationToken ct) =>
        Ok(await _mediator.Send(new SyncPermissionCatalogCommand(), ct));

    [HttpGet("users/{userId}")]
    public async Task<IActionResult> GetUser(string userId, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetUserPermissionsQuery { UserId = userId }, ct));

    [HttpDelete("users/{userId}")]
    public async Task<IActionResult> ClearUser(string userId, [FromQuery] string resource, [FromQuery] string action, CancellationToken ct)
    {
        await _mediator.Send(new ClearUserPermissionCommand { UserId = userId, Resource = resource, Action = action }, ct);
        return NoContent();
    }

    [HttpPut("users/{userId}")]
    public async Task<IActionResult> SetUser(string userId, [FromBody] PermissionInput input, CancellationToken ct) =>
        Ok(await _mediator.Send(new SetUserPermissionCommand { UserId = userId, Resource = input.Resource, Action = input.Action, Allowed = input.Allowed }, ct));

    [HttpGet("roles/{roleCode}")]
    public async Task<IActionResult> GetRole(string roleCode, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetRolePermissionsQuery { RoleCode = roleCode }, ct));

    [HttpPost("role-baseline")]
    public async Task<IActionResult> RoleBaseline([FromBody] RoleBaselineRequest input, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetRoleBaselineQuery { RoleCodes = input.RoleCodes }, ct));

    [HttpPut("roles/{roleCode}")]
    public async Task<IActionResult> SetRole(string roleCode, [FromBody] PermissionInput input, CancellationToken ct) =>
        Ok(await _mediator.Send(new SetRolePermissionCommand { RoleCode = roleCode, Resource = input.Resource, Action = input.Action, Allowed = input.Allowed }, ct));
}
