namespace ProjectAPI.Domain.Sales.Entities;

/// <summary>
/// SAV/warranty claim lifecycle (spec §20, §47.5).
///
/// Ten states, not the previous five: qualification, technician assignment,
/// a buyer-facing "more info requested" loop, an in-progress/waiting-customer
/// pair, reopening, and cancellation all needed a state that did not exist
/// before. <see cref="ClaimStateMachine"/> is the only place transitions are
/// decided — handlers must never assign <see cref="AfterSaleClaim.Status"/>
/// directly.
/// </summary>
public enum ClaimStatus
{
    Submitted = 0,
    UnderReview = 1,
    MoreInfoRequired = 2,
    Assigned = 3,
    InProgress = 4,
    WaitingCustomer = 5,
    Resolved = 6,
    Rejected = 7,
    Cancelled = 8,
    Closed = 9
}

public enum ClaimPriority { Low = 0, Normal = 1, High = 2, Critical = 3 }
public enum ClaimCategory { General = 0, Electricity = 1, Plumbing = 2, Finishing = 3, DoorsWindows = 4, Other = 99 }

public class AfterSaleClaim
{
    public Guid Id { get; set; }

    // Unit/ownership
    public Guid UnitId { get; set; }
    public Guid? PurchaseId { get; set; }   // optional: tie to Purchase if you want strict “buyer owns this”

    /// <summary>§5.9/§20 — the warranty this claim is admissible under. Required to qualify a claim.</summary>
    public Guid? WarrantyId { get; set; }

    // Who opened (registered or guest)
    public string? BuyerId { get; set; }    // AspNetUsers.Id
    public string? GuestName { get; set; }
    public string? GuestEmail { get; set; }
    public string? GuestPhone { get; set; }

    // Details
    public string Title { get; set; }
    public string Description { get; set; }
    public ClaimCategory Category { get; set; } = ClaimCategory.General;
    public ClaimPriority Priority { get; set; } = ClaimPriority.Normal;
    public ClaimStatus Status { get; set; } = ClaimStatus.Submitted;

    /// <summary>§20 — set by the project admin at qualification; drives SLA targets.</summary>
    public DateTime? SlaTargetAt { get; set; }

    /// <summary>§20 — buyer-requested reopen count, kept across the claim's life.</summary>
    public int ReopenCount { get; set; }

    // Assignment & lifecycle
    public string? AssignedAgentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ResolutionSummary { get; set; } // filled on resolve

    // Navs
    public ICollection<ClaimAttachment> Attachments { get; set; } = new List<ClaimAttachment>();
    public ICollection<ClaimComment> Comments { get; set; } = new List<ClaimComment>();
    public ICollection<ClaimHistory> History { get; set; } = new List<ClaimHistory>();
}

/// <summary>
/// Transition rules for the SAV claim lifecycle (§20, §47.5):
/// <code>
/// SUBMITTED → UNDER_REVIEW → ASSIGNED → IN_PROGRESS → RESOLVED → CLOSED
/// UNDER_REVIEW → MORE_INFO_REQUIRED → UNDER_REVIEW      UNDER_REVIEW → REJECTED
/// IN_PROGRESS ⇄ WAITING_CUSTOMER      RESOLVED → IN_PROGRESS (reopen)
/// SUBMITTED|MORE_INFO_REQUIRED → CANCELLED      terminal: REJECTED, CANCELLED, CLOSED
/// </code>
/// Any transition outside this matrix throws <c>INVALID_STATUS_TRANSITION</c>.
/// </summary>
public static class ClaimStateMachine
{
    // Spec order: SOUMISE → ASSIGNÉE → EN_COURS_D_EXAMEN → EN_RÉSOLUTION →
    // RÉSOLUE → VALIDÉE/FERMÉE (Submitted → Assigned → UnderReview → InProgress →
    // Resolved → Closed). The technical lead assigns straight from SUBMITTED;
    // the assigned technician examines, then resolves. Side paths: more info
    // from the buyer during examination, waiting for the customer during the
    // resolution, rejection before any work, reopening a resolved claim.
    private static readonly IReadOnlyDictionary<ClaimStatus, ClaimStatus[]> Allowed =
        new Dictionary<ClaimStatus, ClaimStatus[]>
        {
            [ClaimStatus.Submitted] = new[] { ClaimStatus.Assigned, ClaimStatus.Rejected, ClaimStatus.Cancelled },
            [ClaimStatus.Assigned] = new[] { ClaimStatus.UnderReview, ClaimStatus.Cancelled },
            [ClaimStatus.UnderReview] = new[] { ClaimStatus.InProgress, ClaimStatus.MoreInfoRequired, ClaimStatus.Rejected },
            [ClaimStatus.MoreInfoRequired] = new[] { ClaimStatus.UnderReview, ClaimStatus.Cancelled },
            [ClaimStatus.InProgress] = new[] { ClaimStatus.WaitingCustomer, ClaimStatus.Resolved },
            [ClaimStatus.WaitingCustomer] = new[] { ClaimStatus.InProgress },
            [ClaimStatus.Resolved] = new[] { ClaimStatus.InProgress, ClaimStatus.Closed },
            [ClaimStatus.Rejected] = Array.Empty<ClaimStatus>(),
            [ClaimStatus.Cancelled] = Array.Empty<ClaimStatus>(),
            [ClaimStatus.Closed] = Array.Empty<ClaimStatus>()
        };

    public static bool CanTransition(ClaimStatus from, ClaimStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static void EnsureCanTransition(ClaimStatus from, ClaimStatus to)
    {
        if (from != to && !CanTransition(from, to))
        {
            throw new InvalidClaimTransitionException(from, to);
        }
    }

    /// <summary>Statuses where the claim is still open and being worked.</summary>
    public static readonly ClaimStatus[] ActiveStatuses =
    {
        ClaimStatus.Submitted,
        ClaimStatus.UnderReview,
        ClaimStatus.MoreInfoRequired,
        ClaimStatus.Assigned,
        ClaimStatus.InProgress,
        ClaimStatus.WaitingCustomer,
        ClaimStatus.Resolved
    };
}

/// <summary>Thrown when a claim status change is refused by <see cref="ClaimStateMachine"/>.</summary>
public class InvalidClaimTransitionException : Exception
{
    public ClaimStatus From { get; }
    public ClaimStatus To { get; }

    public InvalidClaimTransitionException(ClaimStatus from, ClaimStatus to)
        : base($"Transition de réclamation non autorisée : {from} → {to}.")
    {
        From = from;
        To = to;
    }
}
