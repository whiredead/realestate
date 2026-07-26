using Microsoft.AspNetCore.Authorization;
using ProjectAPI.Api.Application.Notary.Appointments.CreateNotaryAppointment;
using ProjectAPI.Api.Application.Notary.Appointments.GetNotaryAppointmentById;
using ProjectAPI.Api.Application.Notary.Appointments.GetNotaryAppointments;
using ProjectAPI.Api.Application.Notary.GetNotaryCalendar;
using ProjectAPI.Api.Application.NotaryAppointments.UpdateNotaryAppointment;

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
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateNotaryAppointment([FromBody] CreateNotaryAppointmentCommand command)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var response = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetNotaryAppointmentById), new { id = response.Id }, response);
    }

    /// <summary>
    /// Retrieves a notary appointment by ID.
    /// </summary>
    /// <param name="id">The ID of the notary appointment.</param>
    /// <returns>The appointment details.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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

        var response = await _mediator.Send(command);

        if (!response.Success)
        {
            return BadRequest(response.Message);
        }

        return Ok(response);
    }
}
