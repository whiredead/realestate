using ProjectAPI.Api.Application.Reservations.ApproveReservation;
using ProjectAPI.Api.Application.Reservations.AssignNotaireToReservation;
using ProjectAPI.Api.Application.Reservations.CancelReservation;
using ProjectAPI.Api.Application.Reservations.CreateReservation;
using ProjectAPI.Api.Application.Reservations.GetReservationById;
using ProjectAPI.Api.Application.Reservations.GetReservations;
using ProjectAPI.Api.Application.Reservations.RejectReservation;
using ProjectAPI.Api.Application.Reservations.RequestChanges;
using ProjectAPI.Api.Application.Reservations.ResubmitReservation;
using ProjectAPI.Api.Application.Reservations.SoldReservation;

namespace ProjectAPI.Api.Controllers;
[ApiController]
[Route("api/[controller]")]
public class ReservationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReservationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("create")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateReservation([FromBody] CreateReservationCommand command)
    {
        var response = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetReservationById), new { id = response.ReservationId }, response);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReservationById(Guid id)
    {
        var response = await _mediator.Send(new GetReservationByIdQuery { ReservationId = id });
        return Ok(response);
    }

    [HttpGet("list")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetReservations([FromQuery] GetReservationsQuery query)
    {
        var response = await _mediator.Send(query);
        return Ok(response);
    }
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveReservationCommand body)
    {
        body.ReservationId = id;
        return Ok(await _mediator.Send(body));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectReservationCommand body)
    {
        body.ReservationId = id;
        return Ok(await _mediator.Send(body));
    }

    /// <summary>
    /// Sends a submitted reservation back to the agent for correction (§12.2).
    /// The unit stays blocked.
    /// </summary>
    [HttpPost("{id:guid}/request-changes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestChanges(Guid id, [FromBody] RequestChangesCommand body)
    {
        body.ReservationId = id;
        return Ok(await _mediator.Send(body));
    }

    /// <summary>
    /// Submits a draft, or returns a corrected reservation for a new decision (§12.4).
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(Guid id, [FromBody] ResubmitReservationCommand body)
    {
        body.ReservationId = id;
        return Ok(await _mediator.Send(body));
    }

    [HttpPut("{id:guid}/assign-notaire")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignNotaire(Guid id, [FromBody] AssignNotaireToReservationCommand body)
    {
        body.ReservationId = id;
        var response = await _mediator.Send(body);
        return Ok(response);
    }

    [HttpPut("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelReservation(Guid id, [FromBody] CancelReservationCommand body)
    {
        body.ReservationId = id;
        var response = await _mediator.Send(body);
        return Ok(response);
    }

    [HttpPut("{id:guid}/sold")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SoldReservation(Guid id, [FromBody] SoldReservationCommand body)
    {
        body.ReservationId = id;
        var response = await _mediator.Send(body);
        return Ok(response);
    }
}
