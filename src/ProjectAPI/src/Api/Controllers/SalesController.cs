using Microsoft.Extensions.Logging;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Sales.AddPayment;
using ProjectAPI.Api.Application.Sales.GetAllSales;
using ProjectAPI.Api.Application.Sales.GetSalesByUser;
using ProjectAPI.Api.Application.Sales.SaleDrafts;
using ProjectAPI.Api.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;

namespace ProjectAPI.Api.Controllers;

[ApiController]
[Route("api/sales")]
[Authorize] // Writes are notary/admin (per-action); a buyer may read their own sales.
public class SalesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<SalesController> _logger;

    public SalesController(IMediator mediator, ILogger<SalesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    // The old POST /api/sales was unrouted in work order #2 Part A because it
    // wrote a Sale row from arbitrary request data — any unit, any buyer, any
    // price, no reservation, no final visit — bypassing the reservation state
    // machine entirely, exactly like the frontend's old "Marquer comme vendu"
    // button. Its handler (Application/Sales/CreateSale) has now been DELETED
    // rather than left unrouted: MediatR registers by assembly scan, so the
    // ungated path stayed one [HttpPost] away from being live again.
    //
    // The four actions below are its gated replacement (§5.7, §6): a sale is
    // opened FROM an approved reservation, on a project in delivery, whose
    // final visit is cleared, and there is at most one active sale per
    // reservation and per unit. The notary's PURCHASE_COMPLETED outcome remains
    // the only thing that CONFIRMS a sale — nothing here writes Confirmed.
    //
    // POST {saleId}/payments stays removed: the immutable payment ledger
    // (§14.3) is not written from this controller. Nothing is ever hard-deleted
    // either (§9) — a sale is abandoned by cancelling it.

    /// <summary>Opens a sale draft on an approved reservation (§5.7, §6).</summary>
    [HttpPost]
    [Authorize(Roles = RoleGroups.AdminsAgents)]
    [ProducesResponseType(typeof(SaleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateDraft([FromBody] CreateSaleDraftCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetByReservation), new { reservationId = result.ReservationId }, result);
    }

    /// <summary>Whether a sale draft may be opened on this reservation, with the reasons when not.</summary>
    [HttpGet("eligibility/{reservationId:guid}")]
    [Authorize(Roles = RoleGroups.AdminsAgents)]
    [ProducesResponseType(typeof(SaleEligibilityResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligibility(Guid reservationId) =>
        Ok(await _mediator.Send(new GetSaleEligibilityQuery { ReservationId = reservationId }));

    /// <summary>
    /// The sale attached to a reservation, or 200 with a null body when there is
    /// none — see <see cref="GetSaleByReservationQuery"/> for why that is not a 404.
    /// A buyer may read their own file; the handler enforces both shapes of scope.
    /// </summary>
    [HttpGet("by-reservation/{reservationId:guid}")]
    // §6.3 — commercial side plus the buyer. The handler's §6.4 project scope
    // is not a substitute: it classes TECHNICIAN as internal staff, so a
    // technician holding any membership on the project read the whole sale,
    // buyer CIN and price included.
    [Authorize(Roles = RoleGroups.SaleReaders)]
    [ProducesResponseType(typeof(SaleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByReservation(Guid reservationId)
    {
        var result = await _mediator.Send(new GetSaleByReservationQuery { ReservationId = reservationId });

        // "This reservation has no sale yet" is a normal answer with a body of
        // `null`, not an absence of content. Ok(null) does not produce that:
        // HttpNoContentOutputFormatter turns a null value into 204, which
        // carries no body at all, so the client has nothing to parse and its
        // `?? null` never fires. JsonResult writes a literal `null` with 200,
        // which is what the endpoint documents and what salesApi expects.
        return result is null ? new JsonResult(result) : Ok(result);
    }

    /// <summary>Corrects a Draft or PendingNotary sale (§6).</summary>
    [HttpPut("{saleId:guid}")]
    [Authorize(Roles = RoleGroups.AdminsAgents)]
    [ProducesResponseType(typeof(SaleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateDraft(Guid saleId, [FromBody] UpdateSaleDraftCommand command)
    {
        // The route segment is authoritative; a body that disagrees is ignored
        // rather than trusted to address a different sale than the URL names.
        command.SaleId = saleId;
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>Abandons a sale that has not been finalised (§6, §9). The row is kept.</summary>
    [HttpPost("{saleId:guid}/cancel")]
    [Authorize(Roles = RoleGroups.AdminsAgents)]
    [ProducesResponseType(typeof(SaleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelDraft(Guid saleId, [FromBody] CancelSaleDraftCommand? command)
    {
        var cancel = command ?? new CancelSaleDraftCommand();
        cancel.SaleId = saleId;
        var result = await _mediator.Send(cancel);
        return Ok(result);
    }

    /// <summary>
    /// Company-wide sales for the admin console. GetByUser below answers "what
    /// did I buy?" and is correct for a buyer, but it left an administrator's
    /// /admin/sales page permanently empty — an admin has bought nothing. This
    /// returns what the company has sold, scoped to the caller's own project
    /// perimeter (§6.4).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = RoleGroups.AdminsAgents)]
    [ProducesResponseType(typeof(AllSalesResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] GetAllSalesQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("user/{userId}")]
    // §6.3 — see SaleReaders. The handler pins a BUYER to their own id, but
    // treats every internal role as staff, so a TECHNICIAN could pass any
    // buyer's id and read that buyer's entire purchase and payment history
    // across every project — no membership required.
    [Authorize(Roles = RoleGroups.SaleReaders)]
    [ProducesResponseType(typeof(UserSalesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetByUser(string userId)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("[Sales.GetByUser][{RequestId}] UserId={UserId}", requestId, userId);

        // No local catch: BusinessRuleException (403 BUYER_SCOPE_DENIED),
        // NotFoundException and the rest reach ApiExceptionFilter, which maps
        // them to typed problem responses. A catch-all here turned a refused
        // access into a 500.
        var result = await _mediator.Send(new GetSalesByUserQuery { UserId = userId });
        _logger.LogInformation("[Sales.GetByUser][{RequestId}] Found {Count} sales", requestId, result.Sales?.Count ?? 0);
        return Ok(result);
    }
}

/// <summary>
/// Standard error response for API errors with debugging information.
/// </summary>
public class ErrorResponse
{
    public string RequestId { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<string>? Details { get; set; }
    public DateTime Timestamp { get; set; }
}