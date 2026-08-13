using Microsoft.AspNetCore.Authorization;
using ProjectAPI.Api.Application.Internal.ProvisionUser;
using ProjectAPI.Infrastructure.Security;

namespace ProjectAPI.Api.Controllers;

/// <summary>
/// Service-to-service endpoints for AuthenticationAPI only. Authenticated by
/// a shared static API key (see InternalApiKeyAuthenticationHandler), not
/// the JWT bearer scheme every other controller uses. Mirror direction of
/// AuthenticationAPI's own InternalController.
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

    /// <summary>Mirrors an AuthenticationAPI account into this service's own AspNetUsers table. Idempotent.</summary>
    [HttpPost("users/provision")]
    public async Task<IActionResult> ProvisionUser([FromBody] ProvisionUserCommand command)
    {
        var response = await _mediator.Send(command);
        return Ok(response);
    }
}
