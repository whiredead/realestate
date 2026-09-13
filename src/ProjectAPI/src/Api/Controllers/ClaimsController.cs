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
using ProjectAPI.Domain.Users.Entities;
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
        // BuyerId is taken from the token, NOT merely defaulted from it.
        //
        // It used to be filled in only when the body left it blank, so a buyer
        // could send someone else's id and have the claim attributed to them:
        // the ownership check downstream verifies that THAT id owns the unit,
        // which it does, so the impersonation passed every check. §6.4 —
        // "un acheteur ne peut accéder qu'à ses propres données".
        //
        // An internal caller (admin/technician) may still file on a buyer's
        // behalf, which is a real workflow — a claim phoned in to the SAV desk.
        // Only a buyer/prospect caller is pinned to themselves.
        //
        // MUST go through ICurrentUser, not a raw ClaimTypes.NameIdentifier
        // lookup — this system's JWTs carry the id under a custom "userId"
        // claim, which ClaimTypes.NameIdentifier never matches (see CurrentUser.cs).
        var isInternal = _currentUser.Roles.Any(r => RoleCodes.Internal.Contains(r, StringComparer.Ordinal));

        if (!isInternal && !string.IsNullOrEmpty(_currentUser.UserId))
        {
            cmd.BuyerId = _currentUser.UserId;
        }
        else if (string.IsNullOrWhiteSpace(cmd.BuyerId) && !string.IsNullOrEmpty(_currentUser.UserId))
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
    /// §20/§24.2 — attaches evidence (photo, short video, PDF) to a claim.
    ///
    /// Files, not URLs: the create-claim command used to accept a list of
    /// client-supplied addresses and store them verbatim. Uploads land in a
    /// private container and are read back only through the action below.
    /// Not role-gated here — a buyer attaches evidence to their OWN claim; the
    /// handler enforces which claims that is.
    /// </summary>
    [HttpPost("{claimId:guid}/attachments")]
    [ProducesResponseType(typeof(Application.Sales.AfterSales.Attachments.ClaimAttachmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadAttachment(
        Guid claimId,
        [FromForm] Application.Sales.AfterSales.Attachments.UploadClaimAttachmentCommand command,
        CancellationToken ct)
    {
        if (command.File is null || command.File.Length == 0)
        {
            throw new Application.Common.Exceptions.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure("File", "Aucun fichier n'a \u00e9t\u00e9 envoy\u00e9.")
            });
        }

        command.ClaimId = claimId;
        return Ok(await _mediator.Send(command, ct));
    }

    /// <summary>
    /// Streams one attachment to a caller entitled to see the claim. The blob
    /// address itself is never handed to the client.
    /// </summary>
    [HttpGet("attachments/{attachmentId:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachment(Guid attachmentId, CancellationToken ct)
    {
        var file = await _mediator.Send(
            new Application.Sales.AfterSales.Attachments.DownloadClaimAttachmentQuery
            {
                AttachmentId = attachmentId
            }, ct);

        return File(file.Content, file.ContentType, file.FileName);
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
    /// <summary>Units the signed-in buyer may file a claim on (confirmed sale, delivered, active warranty), with their location.</summary>
    [HttpGet("eligible-units")]
    public async Task<IActionResult> GetEligibleUnits(CancellationToken ct) =>
        Ok(await _mediator.Send(new Application.Sales.AfterSales.ClaimDetail.GetClaimEligibleUnitsQuery(), ct));

    /// <summary>Full claim: description, history, comments, attachments (with phase) and the unit's context.</summary>
    [HttpGet("{claimId:guid}")]
    public async Task<IActionResult> GetDetail(Guid claimId, CancellationToken ct) =>
        Ok(await _mediator.Send(new Application.Sales.AfterSales.ClaimDetail.GetClaimDetailQuery { ClaimId = claimId }, ct));

    /// <summary>Adds a message: work done / note by staff, reply by the buyer.</summary>
    [HttpPost("{claimId:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid claimId, [FromBody] Application.Sales.AfterSales.ClaimDetail.AddClaimCommentCommand command, CancellationToken ct)
    {
        command.ClaimId = claimId;
        return Ok(await _mediator.Send(command, ct));
    }

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
