using Microsoft.AspNetCore.Authorization;
using ProjectAPI.Api.Application.Common.Crm.AcceptInvitation;

namespace ProjectAPI.Api.Controllers;

/// <summary>
/// §1.1/§6.2 Phase 2 — public because the invitee has no session yet: the
/// token itself is what proves they were approved as a buyer (same posture
/// as AuthenticationAPI's confirm-email/reset-password endpoints).
/// </summary>
[ApiController]
[Route("api/invitations")]
[AllowAnonymous]
public class InvitationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public InvitationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("accept")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Accept([FromBody] AcceptInvitationCommand command)
        => Ok(await _mediator.Send(command));
}
