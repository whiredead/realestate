using ProjectAPI.Api.Application.Payments.CreatePaymentSchedule;
using ProjectAPI.Api.Application.Payments.GetPaymentSchedule;
using ProjectAPI.Api.Application.Payments.RecordPayment;
using ProjectAPI.Api.Application.Payments.ReversePayment;
using ProjectAPI.Domain.Payments.Entities;
using ProjectAPI.Api.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;

namespace ProjectAPI.Api.Controllers;

/// <summary>
/// Payment schedules and financial entries (spec §14).
///
/// GPIA does not collect money: these endpoints record what the administration
/// declares it has received (§14.1). Online payment is out of scope (§4.3).
/// </summary>
[ApiController]
[Route("api/payments")]
[Authorize] // §6.3 Paiements: buyer reads own (L propre), admin writes (C/M périmètre).
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PaymentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Accepted payment method codes, for populating a UI selector.</summary>
    [HttpGet("method-codes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetMethodCodes() => Ok(PaymentMethodCodes.All);

    /// <summary>Schedule, installments and derived totals for a reservation (§14.3).</summary>
    [HttpGet("reservations/{reservationId:guid}/schedule")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSchedule(Guid reservationId)
    {
        return Ok(await _mediator.Send(new GetPaymentScheduleQuery { ReservationId = reservationId }));
    }

    /// <summary>Creates a schedule, optionally activating it (§14.2).</summary>
    [HttpPost("reservations/{reservationId:guid}/schedule")]
    [Authorize(Roles = RoleGroups.Admins)] // §6.3 "C/M périmètre".
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateSchedule(Guid reservationId, [FromBody] CreatePaymentScheduleCommand body)
    {
        body.ReservationId = reservationId;
        return Ok(await _mediator.Send(body));
    }

    /// <summary>Records a received payment (§14.3).</summary>
    [HttpPost("reservations/{reservationId:guid}/payments")]
    [Authorize(Roles = RoleGroups.Admins)] // §6.3 "C/M périmètre".
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RecordPayment(Guid reservationId, [FromBody] RecordPaymentCommand body)
    {
        body.ReservationId = reservationId;
        return Ok(await _mediator.Send(body));
    }

    /// <summary>Reverses a validated payment (§14.3). The original is never edited.</summary>
    [HttpPost("{paymentId:guid}/reverse")]
    [Authorize(Roles = RoleGroups.Admins)] // §14.3 — reversal is an admin action.
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReversePayment(Guid paymentId, [FromBody] ReversePaymentCommand body)
    {
        body.PaymentId = paymentId;
        return Ok(await _mediator.Send(body));
    }
}
