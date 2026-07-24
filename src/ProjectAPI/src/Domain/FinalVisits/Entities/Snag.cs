namespace ProjectAPI.Domain.FinalVisits.Entities;

/// <summary>
/// Final-visit report (spec §17.3, §48.8). Versioned: a correction creates a new
/// version, and the buyer acknowledges a specific version.
/// </summary>
public class FinalVisitReport
{
    public Guid Id { get; set; }

    public Guid AppointmentId { get; set; }

    public int VersionNo { get; set; } = 1;

    public ReportStatus Status { get; set; } = ReportStatus.Draft;

    public VisitResult ResultCode { get; set; }

    public string? GeneralCondition { get; set; }
    public string? Observations { get; set; }

    public DateTime? SubmittedAt { get; set; }

    /// <summary>
    /// Buyer acknowledgement (§17.3 FR-FVI-007). This is a simple in-app
    /// confirmation, explicitly NOT a certified electronic signature.
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public string? DisputeReason { get; set; }

    public string? AuthorUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Snag> Snags { get; set; } = new List<Snag>();
}

/// <summary>Report lifecycle, tracked separately from the appointment (§17.5).</summary>
public enum ReportStatus
{
    Draft = 0,
    Submitted = 1,
    AwaitingBuyerAcknowledgement = 2,
    Acknowledged = 3,
    Disputed = 4,
    Superseded = 5
}

/// <summary>Outcome of a final visit (§17.3 FR-FVI-005).</summary>
public enum VisitResult
{
    CompliantNoSnag = 0,
    CompliantMinorSnags = 1,
    NonCompliantMajorSnags = 2,
    NonCompliantBlockingSnags = 3
}

/// <summary>
/// A defect recorded during the final visit (spec §17.4, §48.8).
///
/// Snags belong to the SALES agent, never to the after-sales technician
/// (§17.4 FR-FVI-010) — that distinction is what separates pre-delivery snags
/// from post-delivery warranty claims.
/// </summary>
public class Snag
{
    public Guid Id { get; set; }

    public Guid ReportId { get; set; }
    public FinalVisitReport Report { get; set; } = null!;

    /// <summary>Human-readable unique code.</summary>
    public string Code { get; set; } = string.Empty;

    public string? CategoryCode { get; set; }

    public SnagSeverity Severity { get; set; } = SnagSeverity.Minor;

    public string Description { get; set; } = string.Empty;
    public string? Location { get; set; }

    public DateTime? TargetResolutionDate { get; set; }

    public SnagStatus Status { get; set; } = SnagStatus.Open;

    public string? ResponsibleSalesAgentId { get; set; }
    public string? ResolutionComment { get; set; }
    public string? ProofUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Snag severity (§17.4 FR-FVI-009).</summary>
public enum SnagSeverity
{
    /// <summary>Does not prevent the notary appointment.</summary>
    Minor = 0,

    /// <summary>Must be resolved and validated before the notary.</summary>
    Major = 1,

    /// <summary>Blocks the notary appointment outright.</summary>
    Blocking = 2
}

/// <summary>Snag lifecycle (§17.4 FR-FVI-011).</summary>
public enum SnagStatus
{
    Open = 0,
    Acknowledged = 1,
    InResolution = 2,
    Resolved = 3,
    Validated = 4,
    Closed = 5
}

/// <summary>Append-only snag history (§48.8).</summary>
public class SnagHistory
{
    public Guid Id { get; set; }
    public Guid SnagId { get; set; }

    /// <summary>
    /// Navigation to the parent snag. Required — not just for querying: EF Core
    /// uses navigation properties (not bare FK scalars) to infer insert order
    /// within a single SaveChangesAsync batch. Without it, EF could insert this
    /// row before its Snag row and violate FK_SnagHistories_Snags.
    /// </summary>
    public Snag Snag { get; set; } = null!;

    public SnagStatus? FromStatus { get; set; }
    public SnagStatus ToStatus { get; set; }
    public string? ActorUserId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string? Comment { get; set; }
}

/// <summary>Snag transition rules (§17.4 FR-FVI-011).</summary>
public static class SnagStateMachine
{
    private static readonly IReadOnlyDictionary<SnagStatus, SnagStatus[]> Allowed =
        new Dictionary<SnagStatus, SnagStatus[]>
        {
            [SnagStatus.Open] = new[] { SnagStatus.Acknowledged, SnagStatus.InResolution },
            [SnagStatus.Acknowledged] = new[] { SnagStatus.InResolution },
            [SnagStatus.InResolution] = new[] { SnagStatus.Resolved },
            // Proof rejected sends it back into resolution (§17.4).
            [SnagStatus.Resolved] = new[] { SnagStatus.Validated, SnagStatus.InResolution },
            [SnagStatus.Validated] = new[] { SnagStatus.Closed },
            [SnagStatus.Closed] = Array.Empty<SnagStatus>()
        };

    public static bool CanTransition(SnagStatus from, SnagStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>
    /// A snag stays ACTIVE until it is VALIDATED or CLOSED (§17.4).
    ///
    /// This is the subtle rule: a major snag merely marked RESOLVED still blocks
    /// the notary — it must be validated first.
    /// </summary>
    public static bool IsActive(SnagStatus status) =>
        status is not (SnagStatus.Validated or SnagStatus.Closed);
}
