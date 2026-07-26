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

    [HttpPost]
    [ProducesResponseType(typeof(CreateSaleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    [Authorize(Roles = RoleGroups.AdminsNotary)] // §12.4 — conversion to sale is recorded by notary/admin.
    public async Task<IActionResult> Create([FromBody] CreateSaleCommand cmd)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("[Sales.Create][{RequestId}] Received request: {Request}",
            requestId, JsonSerializer.Serialize(cmd));

        try
        {
            var result = await _mediator.Send(cmd);
            _logger.LogInformation("[Sales.Create][{RequestId}] Success: SaleId={SaleId}, PurchaseId={PurchaseId}",
                requestId, result.SaleId, result.PurchaseId);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "[Sales.Create][{RequestId}] Not found: {Message}", requestId, ex.Message);
            return NotFound(new ErrorResponse
            {
                RequestId = requestId,
                Error = "NotFound",
                Message = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (FluentValidation.ValidationException ex)
        {
            var errors = ex.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            _logger.LogWarning(ex, "[Sales.Create][{RequestId}] Validation failed: {Errors}",
                requestId, string.Join("; ", errors));
            return BadRequest(new ErrorResponse
            {
                RequestId = requestId,
                Error = "ValidationFailed",
                Message = "One or more validation errors occurred.",
                Details = errors,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Sales.Create][{RequestId}] Internal error: {Message}\nStackTrace: {StackTrace}",
                requestId, ex.Message, ex.StackTrace);
            return StatusCode(500, new ErrorResponse
            {
                RequestId = requestId,
                Error = "InternalServerError",
                Message = ex.Message,
                Details = new List<string>
                {
                    $"ExceptionType: {ex.GetType().Name}",
                    $"InnerException: {ex.InnerException?.Message ?? "None"}",
                    $"StackTrace: {ex.StackTrace}"
                },
                Timestamp = DateTime.UtcNow
            });
        }
    }

    [HttpPost("{saleId:guid}/payments")]
    [Authorize(Roles = RoleGroups.Admins)] // §6.3 Paiements: "C/M périmètre" — admin records payments.
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddPayment(Guid saleId, [FromBody] AddPaymentCommand cmd)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        _logger.LogInformation("[Sales.AddPayment][{RequestId}] SaleId={SaleId}, Amount={Amount}",
            requestId, saleId, cmd.AmountPaid);

        try
        {
            cmd.SaleId = saleId;
            var result = await _mediator.Send(cmd);
            _logger.LogInformation("[Sales.AddPayment][{RequestId}] Success: {Result}", requestId, result);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "[Sales.AddPayment][{RequestId}] Not found: {Message}", requestId, ex.Message);
            return NotFound(new ErrorResponse
            {
                RequestId = requestId,
                Error = "NotFound",
                Message = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Sales.AddPayment][{RequestId}] Internal error: {Message}", requestId, ex.Message);
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