using ProjectAPI.Api.Application.Sales.AfterSales.CreateAfterSaleClaim;
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
    public string ChangedByUserId { get; set; }
    public string? Note { get; set; }                 // e.g. reason or progress note
    public string? ResolutionSummary { get; set; }    // required if Resolved
    public List<FileDto> Proofs { get; set; } = new(); // attachments when resolving

    /// <summary>Required when qualifying to ASSIGNED (§20 — project admin sets priority + technician).</summary>
    public string? AssignedAgentId { get; set; }

    /// <summary>Optional at qualification; when set, drives SLA detection (§49.5).</summary>
    public DateTime? SlaTargetAt { get; set; }

    public ClaimPriority? Priority { get; set; }
}
