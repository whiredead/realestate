using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.Sales.AfterSales.CreateAfterSaleClaim;
using ProjectAPI.Api.Application.Sales.AfterSales.GetClaims;
using ProjectAPI.Api.Application.Sales.AfterSales.GetMyClaims;
using ProjectAPI.Api.Application.Sales.AfterSales.RespondToClaimResolution;
using ProjectAPI.Api.Application.Sales.AfterSales.UpdateClaimStatus;
using ProjectAPI.Api.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using ValidationException = FluentValidation.ValidationException;

namespace ProjectAPI.Api.Controllers;

[ApiController]
[Route("api/after-sales/claims")]
[Authorize] // §6.3 SAV: buyer creates own (C/M propre); technician/admin handle (C/M affecté).
public class AfterSaleClaimsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUser _currentUser;

    public AfterSaleClaimsController(IMediator mediator, ICurrentUser currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Create a new after-sale claim. If BuyerId is not provided, we'll try to use the authenticated user id.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] CreateAfterSaleClaimCommand cmd, CancellationToken ct)
    {
        // If not explicitly set, pull BuyerId from the authenticated caller.
        // MUST go through ICurrentUser, not a raw ClaimTypes.NameIdentifier
        // lookup — this system's JWTs carry the id under a custom "userId"
        // claim, which ClaimTypes.NameIdentifier never matches, silently
        // leaving BuyerId null for every buyer-created claim (see CurrentUser.cs).
        if (string.IsNullOrWhiteSpace(cmd.BuyerId) && !string.IsNullOrEmpty(_currentUser.UserId))
        {
            cmd.BuyerId = _currentUser.UserId;
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
    [Authorize(Roles = RoleGroups.AdminsTechnicians)] // §6.4 — technician sees assigned claims; admin oversees.
    public async Task<ActionResult<PaginatedResponse<AfterSaleClaimResponse>>> Get(
        [FromQuery] GetClaimsQuery query,
        CancellationToken ct)
    {
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>§8 "SAV" — the buyer's own claims only, never a supplied buyer id.</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine([FromQuery] GetMyClaimsQuery query, CancellationToken ct)
    {
        return Ok(await _mediator.Send(query, ct));
    }

    /// <summary>
    /// Update the status of a claim. When resolving, ResolutionSummary and optional proofs are supported.
    /// </summary>
    [HttpPut("{claimId:guid}/status")]
    [Authorize(Roles = RoleGroups.AdminsTechnicians)] // §20 qualification/traitement — technician/admin.
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

    /// <summary>
    /// The buyer's own response to a resolved claim — confirm it (closes it)
    /// or reopen it with a reason. Ownership is checked in-handler; no role
    /// restriction beyond being signed in, since this is reachable only by
    /// the claim's own buyer.
    /// </summary>
    [HttpPost("{claimId:guid}/respond")]
    public async Task<IActionResult> RespondToResolution(
        Guid claimId,
        [FromBody] RespondToClaimResolutionCommand cmd,
        CancellationToken ct)
    {
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
