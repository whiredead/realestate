using Microsoft.AspNetCore.Authorization;
using ProjectAPI.Api.Application.Notifications.GetMyNotifications;
using ProjectAPI.Api.Application.Notifications.MarkNotificationRead;

namespace ProjectAPI.Api.Controllers;

/// <summary>§6.2/§8 — per-user notification inbox. Every action resolves to the caller's own notifications only.</summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("mine")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine([FromQuery] bool unreadOnly = false)
    {
        return Ok(await _mediator.Send(new GetMyNotificationsQuery { UnreadOnly = unreadOnly }));
    }

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        return Ok(await _mediator.Send(new MarkNotificationReadCommand { NotificationId = id }));
    }
}
