using ProjectAPI.Api.Application.FinalVisits.AcknowledgeReport;
using ProjectAPI.Api.Application.FinalVisits.GetNotaryEligibility;
using ProjectAPI.Api.Application.FinalVisits.RequestFinalVisit;
using ProjectAPI.Api.Application.FinalVisits.SubmitFinalVisitReport;
using ProjectAPI.Api.Application.FinalVisits.TransitionSnag;
using ProjectAPI.Api.Application.FinalVisits.TransitionVisitAppointment;
using ProjectAPI.Api.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;

namespace ProjectAPI.Api.Controllers;

/// <summary>
/// Final visits, reports and snags (spec §17), plus the notary-eligibility
/// calculation that gates §18.
/// </summary>
[ApiController]
[Route("api/final-visits")]
[Authorize] // §6.3 Visite finale: buyer requests/acknowledges (own); agent/admin manage (affecté).
public class FinalVisitsController : ControllerBase
{
    private readonly IMediator _mediator;

    public FinalVisitsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Requests a final visit, or a further attempt (§17.1).</summary>
    [HttpPost("reservations/{reservationId:guid}/request")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Request(Guid reservationId, [FromBody] RequestFinalVisitCommand body)
    {
        body.ReservationId = reservationId;
        return Ok(await _mediator.Send(body));
    }

    /// <summary>Confirms, reschedules, rejects, cancels or completes an attempt (§17.2).</summary>
    [Authorize(Roles = RoleGroups.AdminsAgents)] // §17.2 — appointment handling by the commercial agent/admin.
    [HttpPost("appointments/{appointmentId:guid}/transition")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TransitionAppointment(
        Guid appointmentId,
        [FromBody] TransitionVisitAppointmentCommand body)
    {
        body.AppointmentId = appointmentId;
        return Ok(await _mediator.Send(body));
    }

    /// <summary>Submits the visit report and its snags (§17.3).</summary>
    [Authorize(Roles = RoleGroups.AdminsAgents)] // §17.3 report authored by agent/admin.
    [HttpPost("appointments/{appointmentId:guid}/report")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SubmitReport(
        Guid appointmentId,
        [FromBody] SubmitFinalVisitReportCommand body)
    {
        body.AppointmentId = appointmentId;
        return Ok(await _mediator.Send(body));
    }

    /// <summary>Buyer accepts or disputes a report (§17.3 FR-FVI-007).</summary>
    [HttpPost("reports/{reportId:guid}/acknowledge")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Acknowledge(Guid reportId, [FromBody] AcknowledgeReportCommand body)
    {
        body.ReportId = reportId;
        return Ok(await _mediator.Send(body));
    }

    /// <summary>Moves a snag through its lifecycle (§17.4).</summary>
    [Authorize(Roles = RoleGroups.AdminsAgents)] // §17.4 snags followed up by agent/admin (validation §47.4).
    [HttpPost("snags/{snagId:guid}/transition")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TransitionSnag(Guid snagId, [FromBody] TransitionSnagCommand body)
    {
        body.SnagId = snagId;
        return Ok(await _mediator.Send(body));
    }

    /// <summary>
    /// Notary eligibility and its causes (§17.6). The frontend displays this
    /// result and must not recompute it.
    /// </summary>
    [HttpGet("reservations/{reservationId:guid}/notary-eligibility")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> NotaryEligibility(Guid reservationId)
    {
        return Ok(await _mediator.Send(new GetNotaryEligibilityQuery { ReservationId = reservationId }));
    }
}
