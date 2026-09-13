using Microsoft.AspNetCore.Authorization;
using ProjectAPI.Api.Application.Common.Idempotency;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Notary.Appointments.CreateNotaryAppointment;
using ProjectAPI.Api.Application.Notary.Appointments.GetNotaryAppointmentAssignmentHistory;
using ProjectAPI.Api.Application.Notary.Appointments.GetNotaryAppointmentById;
using ProjectAPI.Api.Application.Notary.Appointments.GetNotaryAppointments;
using ProjectAPI.Api.Application.Notary.GetNotaryCalendar;
using ProjectAPI.Api.Application.NotaryAppointments.UpdateNotaryAppointment;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Api.Controllers;

/// <summary>
/// Controller for managing notary appointments.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize] // §6.3 RDV notaire — no anonymous access (was wide open). Buyer/agent/notary/admin per §6.3.
public class NotaryAppointmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotaryAppointmentsController"/> class.
    /// </summary>
    /// <param name="mediator">The mediator instance.</param>
    public NotaryAppointmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Creates a new notary appointment.
    /// </summary>
    /// <param name="command">The command containing appointment details.</param>
    /// <returns>The response containing the created appointment ID.</returns>
    [HttpPost]
    // §5.7 FR-NOT-003 — requested by the buyer, the responsible agent or the
    // project admin. Previously only [Authorize], so any signed-in account
    // could raise one.
    [Authorize(Roles = RoleGroups.NotaryAppointmentRequesters)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateNotaryAppointment([FromBody] CreateNotaryAppointmentCommand command)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        command.IdempotencyKey ??= Request.GetIdempotencyKey();
        var response = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetNotaryAppointmentById), new { id = response.Id }, response);
    }

    /// <summary>
    /// Retrieves a notary appointment by ID.
    /// </summary>
    /// <param name="id">The ID of the notary appointment.</param>
    /// <returns>The appointment details.</returns>
    [HttpGet("{id}")]
    // §6.3 — parties to the deed plus admins. The handler's §6.4 scope check
    // cannot substitute for this: it classes TECHNICIAN as an internal role,
    // so a technician with a membership on the project passed it.
    [Authorize(Roles = RoleGroups.NotaryAppointmentReaders)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNotaryAppointmentById(Guid id)
    {
        var query = new GetNotaryAppointmentByIdQuery { Id = id };
        var response = await _mediator.Send(query);

        if (response == null)
        {
            return NotFound($"Notary appointment with ID {id} not found.");
        }

        return Ok(response);
    }

    /// <summary>
    /// Retrieves a list of notary appointments based on filters.
    /// </summary>
    /// <param name="query">The query containing filters.</param>
    /// <returns>A paginated list of notary appointments.</returns>
    [HttpGet]
    // §6.3 — see NotaryAppointmentReaders. A BUYER is allowed through here and
    // narrowed to their own rows by the handler; a TECHNICIAN is refused at the
    // door, because the handler would class it as staff and hand it the whole
    // project's deed files.
    [Authorize(Roles = RoleGroups.NotaryAppointmentReaders)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetNotaryAppointments([FromQuery] GetNotaryAppointmentsQuery query)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var response = await _mediator.Send(query);
        return Ok(response);
    }

    [HttpGet("NotaireAvailability")]
    // §6.4 — a notary's calendar has no reservation to scope by, so gate it by
    // role instead: internal roles that book/manage notary appointments, plus
    // the notary themself. A buyer/technician has no legitimate reason to
    // browse an arbitrary notary's full schedule.
    [Authorize(Roles = RoleGroups.AdminsAgentsNotary)]
    public async Task<IActionResult> GetNotaireAvailability(
        Guid notaryId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var todayLocal = DateTime.Today;            
        var defaultFrom = todayLocal.AddDays(1);   
        var defaultTo = defaultFrom.AddDays(7);

        var start = (from ?? defaultFrom).Date;
        var end = (to ?? defaultTo).Date;

        if (from.HasValue && !to.HasValue)
            end = start.AddDays(7);

        // guard: ensure end is after start
        if (end <= start)
            return BadRequest("'to' must be after 'from'.");

        var result = await _mediator.Send(new GetNotaryCalendarQuery
        {
            NotaryId = notaryId.ToString(),
            From = start,
            To = end
        });

        return Ok(result);
    }

    /// <summary>
    /// Updates a notary appointment's status, tax fees, and/or tahfid fees.
    /// </summary>
    /// <param name="id">The ID of the notary appointment.</param>
    /// <param name="command">The command containing the fields to update.</param>
    /// <returns>The response indicating success or failure.</returns>
    [HttpPut("{id}")]
    // §18.4 — confirming, rescheduling and recording the OUTCOME are the
    // notary's acts (admins may act for them). An agent or buyer must not be
    // able to declare a purchase finalised: that outcome converts the sale.
    // The responsible sales agent may confirm or cancel the slot (spec: RDV
    // confirmable par agent, admin ou notaire); the handler refuses them the
    // outcome, the completion and the reassignment.
    [Authorize(Roles = RoleGroups.AdminsAgentsNotary)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateNotaryAppointment(Guid id, [FromBody] UpdateNotaryAppointmentCommand command)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Set the ID from the route parameter
        command.Id = id;
        command.ActorUserId ??= User.FindFirst("UserId")?.Value;

        var response = await _mediator.Send(command);

        if (!response.Success)
        {
            return BadRequest(response.Message);
        }

        return Ok(response);
    }

    /// <summary>Full notary assignment/reassignment history, including any prior appointment rows it was chained from.</summary>
    [HttpGet("{id}/assignment-history")]
    [Authorize(Roles = RoleGroups.AdminsNotary)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAssignmentHistory(Guid id)
    {
        var rolesClaim = User?.FindFirst("Roles")?.Value;
        var roles = rolesClaim?.Split(',').Select(r => r.Trim()) ?? Enumerable.Empty<string>();
        var callerId = User?.FindFirst("UserId")?.Value;

        var res = await _mediator.Send(new GetNotaryAppointmentAssignmentHistoryQuery
        {
            NotaryAppointmentId = id,
            RestrictToNotaryId = (roles.Contains(RoleCodes.Notary) || roles.Contains("Notaire")) ? callerId : null
        });
        return Ok(res);
    }
}
