using ProjectAPI.Domain.Sales.Entities;

namespace ProjectAPI.Api.Application.Sales.AfterSales.UpdateClaimStatus;

/// <summary>
/// Drives a SAV claim through the §20 lifecycle. One command covers every
/// transition (qualify, assign, start work, request more info, resolve,
/// reopen, cancel, close) since they share the same guard rails; the target
/// status decides which fields are required.
/// </summary>
public class UpdateClaimStatusCommand : IRequest<bool>
{
    public Guid ClaimId { get; set; }
    public ClaimStatus NewStatus { get; set; }
    /// <summary>
    /// Accepted for wire compatibility and deliberately IGNORED: the history
    /// entry's actor is read from the token (<c>ICurrentUser.UserId</c>), never
    /// from the request body. Do not re-wire this into the handler.
    /// </summary>
    public string ChangedByUserId { get; set; } = string.Empty;

    public string? Note { get; set; }                 // e.g. reason or progress note
    public string? ResolutionSummary { get; set; }    // required if Resolved

    // Proofs are NOT accepted here.
    //
    // This carried a List<FileDto> of client-supplied URLs, stored verbatim as
    // the resolution's evidence — the same hole the create command had. A
    // technician attaches proof of the repair as a FILE, via
    // POST /api/claims/{claimId}/attachments, before or after resolving.

    /// <summary>Required when qualifying to ASSIGNED (§20 — project admin sets priority + technician).</summary>
    public string? AssignedAgentId { get; set; }

    /// <summary>Optional at qualification; when set, drives SLA detection (§49.5).</summary>
    public DateTime? SlaTargetAt { get; set; }

    public ClaimPriority? Priority { get; set; }
}
