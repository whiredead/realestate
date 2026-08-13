using AuthenticationAPI.Api.Application.Internal.GetUserRoles;
using AuthenticationAPI.Api.Application.Internal.ProvisionInternalUser;
using AuthenticationAPI.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;

namespace AuthenticationAPI.Api.Controllers;

/// <summary>
/// Phase 2 — service-to-service endpoints for ProjectAPI only. Authenticated
/// by a shared static API key (see InternalApiKeyAuthenticationHandler), NOT
/// the JWT bearer scheme every other controller uses — a caller with a
/// perfectly valid user JWT still cannot reach these actions, and a caller
/// with the internal key cannot reach any other action (schemes are
/// per-action, not merged). Kept under a distinct "internal/" route prefix
/// so it's trivially excludable from public Swagger/CORS exposure.
/// </summary>
[Route("internal/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = InternalApiKeyDefaults.AuthenticationScheme)]
public class InternalController : ControllerBase
{
    private readonly ISender _mediator;

    public InternalController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Creates or activates an account for an internal role. See ProvisionInternalUserHandler for existing-account handling.</summary>
    [HttpPost("users/provision")]
    public async Task<IActionResult> ProvisionUser([FromBody] ProvisionInternalUserCommand command)
    {
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    /// <summary>Returns the real, canonical roles an account holds — the source of truth for ProjectAPI's membership-grant role checks.</summary>
    [HttpGet("users/{userId}/roles")]
    public async Task<IActionResult> GetUserRoles(string userId)
    {
        var response = await _mediator.Send(new GetUserRolesQuery { UserId = userId });
        return Ok(response);
    }
}
