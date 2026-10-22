using Microsoft.AspNetCore.Authorization;
using ProjectAPI.Api.Application.Identity.Session;

namespace ProjectAPI.Api.Controllers;

[ApiController, Route("api/session")]
public sealed class SessionController : ControllerBase
{
    private readonly IMediator _mediator;
    public SessionController(IMediator mediator) => _mediator = mediator;

    [HttpPost("refresh"), AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest body, CancellationToken ct)
    {
        var tokens = await _mediator.Send(new RefreshSessionCommand { RefreshToken = body.RefreshToken }, ct);
        return tokens is null ? Unauthorized() : Ok(tokens);
    }

    [HttpPost("logout"), AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest body, CancellationToken ct)
    {
        await _mediator.Send(new LogoutSessionCommand { RefreshToken = body.RefreshToken }, ct);
        return NoContent();
    }
}
