using Microsoft.AspNetCore.Authorization;
using ProjectAPI.Api.Application.Appointments.CreateAppointment;
using ProjectAPI.Api.Application.Appointments.GetAppointmentAssignmentHistory;
using ProjectAPI.Api.Application.Appointments.GetAppointmentById;
using ProjectAPI.Api.Application.Appointments.GetAppointments;
using ProjectAPI.Api.Application.Appointments.GetMyAppointments;
using ProjectAPI.Api.Application.Appointments.GetAppointmentVisitReport;
using ProjectAPI.Api.Application.Appointments.GetAppointmentVisitReportForBuyer;
using ProjectAPI.Api.Application.Appointments.SubmitVisitReport;
using ProjectAPI.Api.Application.Appointments.UpdateAppointmentStatus;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "Bearer")]
public class AppointmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AppointmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Creates a new appointment.
    /// </summary>
    /// <param name="command">The appointment creation command.</param>
    /// <returns>A response containing the appointment ID and a success message.</returns>
    [HttpPost]
    [AllowAnonymous] // §6.3 RDV commercial: "C" pour le visiteur — demande publique conservée.
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAppointment([FromBody] CreateAppointmentCommand command)
    {
        var userId = User.FindFirst("UserId")?.Value;
        var rolesClaim = User.FindFirst("Roles")?.Value;
        var roles = rolesClaim?.Split(',').Select(r => r.Trim());

        if (roles != null && roles.Contains("Agent"))
        {
            // If the user is an agent, use the UserId from claims as the AgentId
            command.AgentId = Guid.Parse(userId);

        }
        else if (roles != null && (roles.Contains("Acheteur") || roles.Contains("Visiteur") || roles.Contains("Other")))
        {
            // If the user is an authenticated visitor or buyer
            command.UserId = userId;
        }
        // Anonymous callers no longer need to supply AgentId themselves —
        // CreateAppointmentHandler now auto-assigns an eligible agent
        // (existing owner, else the project's configured rule) when none is
        // given. Previously this rejected every anonymous booking with a 400
        // unless the visitor somehow already knew an agent's id to send.

        var response = await _mediator.Send(command);
        return Ok(response);
    }
    /// <summary>
    /// Retrieves a list of appointments with optional filters.
    /// </summary>
    /// <param name="query">The filters and pagination options for retrieving appointments.</param>
    /// <returns>A paginated response containing a list of appointments.</returns>
    [HttpGet]
    [Authorize(Roles = RoleGroups.AdminsAgents)] // §6.3 — agenda interne, agent/admin.
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAppointments([FromQuery] GetAppointmentsQuery query)
    {
        // No agent pin here: the handler scopes by project perimeter. A sales agent sees every appointment of the
        // projects assigned to them (acting on a colleague's stays restricted); AgentId is an optional filter only.
        var response = await _mediator.Send(query);
        return Ok(response);
    }

    /// <summary>§8 "My appointments" — the buyer's own commercial appointments, never anyone else's.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyAppointments()
    {
        return Ok(await _mediator.Send(new GetMyAppointmentsQuery()));
    }

    /// <summary>Retrieves a single appointment by id.</summary>
    [HttpGet("{appointmentId:guid}")]
    [Authorize(Roles = RoleGroups.AdminsAgents)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAppointmentById(Guid appointmentId)
    {
        var response = await _mediator.Send(new GetAppointmentByIdQuery { AppointmentId = appointmentId });
        return Ok(response);
    }

    /// <summary>
    /// Updates the status of an appointment.
    /// </summary>
    /// <param name="appointmentId">The appointment to update, from the route.</param>
    /// <param name="command">The command to update the appointment status.</param>
    /// <returns>A response indicating the success or failure of the update.</returns>
    [HttpPatch("{appointmentId}/status")]
    [Authorize(Roles = RoleGroups.AdminsAgents)] // §6.3 RDV commercial: "V/M" — agent/admin.
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAppointmentStatus([FromRoute] Guid appointmentId, [FromBody] UpdateAppointmentStatusCommand command)
    {
        // The route segment was never bound onto the command — callers had to
        // duplicate the id in the request body for this to work at all.
        command.AppointmentId = appointmentId;
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    /// <summary>
    /// Full agent assignment/reassignment history for an appointment,
    /// including any prior appointment rows it was chained from (see
    /// PreviousAppointmentId).
    /// </summary>
    [HttpGet("{appointmentId}/assignment-history")]
    [Authorize(Roles = RoleGroups.AdminsAgents)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAssignmentHistory(Guid appointmentId)
    {
        var rolesClaim = User?.FindFirst("Roles")?.Value;
        var roles = rolesClaim?.Split(',').Select(r => r.Trim()) ?? Enumerable.Empty<string>();
        var callerId = User?.FindFirst("UserId")?.Value;

        var res = await _mediator.Send(new GetAppointmentAssignmentHistoryQuery
        {
            AppointmentId = appointmentId,
            // Reading follows the project perimeter enforced in the handler.
            RestrictToAgentId = null
        });
        return Ok(res);
    }

    /// <summary>
    /// Records the sales agent's follow-up report after the visit — the
    /// missing link between "appointment happened" and "reservation
    /// created." Always creates a new version; never edits a submitted one.
    /// </summary>
    [HttpPost("{appointmentId}/visit-report")]
    [Authorize(Roles = RoleGroups.AdminsAgents)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitVisitReport(Guid appointmentId, [FromBody] SubmitAppointmentVisitReportCommand command)
    {
        command.AppointmentId = appointmentId;
        command.AuthorUserId ??= User.FindFirst("UserId")?.Value;
        var response = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetVisitReport), new { appointmentId }, response);
    }

    /// <summary>Full internal view of the latest visit-report version — agent/admin only.</summary>
    [HttpGet("{appointmentId}/visit-report")]
    [Authorize(Roles = RoleGroups.AdminsAgents)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVisitReport(Guid appointmentId)
    {
        var res = await _mediator.Send(new GetAppointmentVisitReportQuery { AppointmentId = appointmentId });
        return res == null ? NotFound() : Ok(res);
    }

    /// <summary>
    /// Buyer-facing summary of the latest visit-report version — omits
    /// InternalNotes, ConfirmedBudget, Objections, and AuthorUserId.
    /// </summary>
    [HttpGet("{appointmentId}/visit-report/mine")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVisitReportForBuyer(Guid appointmentId)
    {
        var res = await _mediator.Send(new GetAppointmentVisitReportForBuyerQuery { AppointmentId = appointmentId });
        return res == null ? NotFound() : Ok(res);
    }

    /*
        /// <summary>
        /// Gets all appointments for a specific user by their user ID.
        /// </summary>
        /// <param name="userId">The user ID to retrieve appointments for.</param>
        /// <returns>A list of appointments associated with the user.</returns>
        [HttpGet("user/{userId}")]
        [Authorize(Roles = "Acheteur,Admin,Agent")]
        public async Task<IActionResult> GetAppointmentsByUserId(string userId)
        {
            var query = new GetAppointmentsByUserIdQuery { UserId = userId };
            var appointments = await _mediator.Send(query);
            return Ok(appointments);
        }

        /// <summary>
        /// Gets all appointments for a specific project by project ID.
        /// </summary>
        /// <param name="projectId">The project ID to retrieve appointments for.</param>
        /// <returns>A list of appointments associated with the project.</returns>
        [HttpGet("project/{projectId}")]
        [Authorize(Roles = "Acheteur,Admin,Agent")]
        public async Task<IActionResult> GetAppointmentsByProjectId(Guid projectId)
        {
            var query = new GetAppointmentsByProjectIdQuery { ProjectId = projectId };
            var appointments = await _mediator.Send(query);
            return Ok(appointments);
        }
    */
}