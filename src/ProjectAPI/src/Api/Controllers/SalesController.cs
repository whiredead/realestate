using Microsoft.Extensions.Logging;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Sales.AddPayment;
using ProjectAPI.Api.Application.Sales.GetSalesByUser;
using ProjectAPI.Domain.Sales.CreateSale;
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

    // POST (create a Sale) and POST {saleId}/payments intentionally removed.
    //
    // §5.3/§5.7: the spec has no Sale entity — "a sale" is a reservation that
    // reached CONVERTED via the notary's PURCHASE_COMPLETED outcome (see
    // UpdateNotaryAppointmentHandler). This endpoint wrote a Sale row directly,
    // with its own PaymentTracking ledger, entirely bypassing the reservation
    // state machine and the immutable Payment ledger (§14.3) — a second,
    // ungated door to the same consequential state change the frontend's old
    // "Marquer comme vendu" button was (removed in work order #2 Part A).
    //
    // Existing Sale/PaymentTracking rows are read-only from here on (GetByUser
    // below) and are never deleted — §9 forbids hard deletion of business data.

    [HttpGet("user/{userId}")]
    [ProducesResponseType(typeof(UserSalesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetByUser(string userId)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("[Sales.GetByUser][{RequestId}] UserId={UserId}", requestId, userId);

        try
        {
            var result = await _mediator.Send(new GetSalesByUserQuery { UserId = userId });
            _logger.LogInformation("[Sales.GetByUser][{RequestId}] Found {Count} sales", requestId, result.Sales?.Count ?? 0);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Sales.GetByUser][{RequestId}] Internal error: {Message}", requestId, ex.Message);
            return StatusCode(500, new ErrorResponse
            {
                RequestId = requestId,
                Error = "InternalServerError",
                Message = ex.Message,
                Details = new List<string>
                {
                    $"ExceptionType: {ex.GetType().Name}",
                    $"InnerException: {ex.InnerException?.Message ?? "None"}"
                },
                Timestamp = DateTime.UtcNow
            });
        }
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