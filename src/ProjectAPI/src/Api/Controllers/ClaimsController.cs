using System.Security.Claims;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.Sales.AfterSales.CreateAfterSaleClaim;
using ProjectAPI.Api.Application.Sales.AfterSales.GetClaims;
using ProjectAPI.Api.Application.Sales.AfterSales.UpdateClaimStatus;
using ValidationException = FluentValidation.ValidationException;

namespace ProjectAPI.Api.Controllers;

[ApiController]
[Route("api/after-sales/claims")]
public class AfterSaleClaimsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AfterSaleClaimsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Create a new after-sale claim. If BuyerId is not provided, we'll try to use the authenticated user id.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] CreateAfterSaleClaimCommand cmd, CancellationToken ct)
    {
        // If not explicitly set, pull BuyerId from the authenticated principal
        if (string.IsNullOrWhiteSpace(cmd.BuyerId))
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(uid))
                cmd.BuyerId = uid;
        }

        try
        {
            var id = await _mediator.Send(cmd, ct);
            // No GetById handler exists, so return 200 with the id payload.
            return Ok(new { id });
        }
        catch (ValidationException ex)
        {
            // Handler throws FluentValidation.ValidationException for business rules
            return ValidationProblem(detail: ex.Message);
        }
    }

    /// <summary>
    /// Get claims with filters and pagination.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<AfterSaleClaimResponse>>> Get(
        [FromQuery] GetClaimsQuery query,
        CancellationToken ct)
    {
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Update the status of a claim. When resolving, ResolutionSummary and optional proofs are supported.
    /// </summary>
    [HttpPut("{claimId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid claimId,
        [FromBody] UpdateClaimStatusCommand cmd,
        CancellationToken ct)
    {
        // Ensure the route id wins over any body value
        cmd.ClaimId = claimId;

        try
        {
            await _mediator.Send(cmd, ct);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ValidationException ex)
        {
            return ValidationProblem(detail: ex.Message);
        }
    }
}
