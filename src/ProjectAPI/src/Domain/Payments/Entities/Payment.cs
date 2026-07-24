namespace ProjectAPI.Domain.Payments.Entities;

/// <summary>
/// An immutable financial entry — spec §14.3, §5.4, §48.6.
///
/// A validated payment is NEVER edited to correct a mistake. The administration
/// records a reversal (contrepassation) linked through
/// <see cref="ReversalOfPaymentId"/> and, if needed, enters a fresh payment.
/// This is what makes the ledger auditable.
///
/// GPIA does not collect money (§14.1): a payment only records what the
/// administration states has been received.
/// </summary>
public class Payment
{
    public Guid Id { get; set; }

    public Guid ReservationId { get; set; }

    public DateTime PaymentDate { get; set; }

    /// <summary>
    /// Signed amount, numeric(15,2). Positive for a payment, negative for a
    /// reversal, so the ledger sums directly.
    /// </summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "MAD";

    /// <summary>Declared method: transfer, cheque, cash… (administered list).</summary>
    public string? MethodCode { get; set; }

    /// <summary>Bank or internal reference. Unique per source when provided (§14.3).</summary>
    public string? ExternalReference { get; set; }

    /// <summary>Where the entry came from: WEB, IMPORT, SYSTEM (§5.3).</summary>
    public string Source { get; set; } = "WEB";

    public PaymentStatus Status { get; set; } = PaymentStatus.PendingValidation;

    /// <summary>Set on a reversal entry, pointing at the payment it cancels.</summary>
    public Guid? ReversalOfPaymentId { get; set; }

    public string? ValidatedBy { get; set; }
    public DateTime? ValidatedAt { get; set; }

    public string? Comment { get; set; }

    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
}

/// <summary>Validation state of a financial entry (§14.3).</summary>
public enum PaymentStatus
{
    /// <summary>Recorded, awaiting administrative validation. Excluded from buyer totals.</summary>
    PendingValidation = 0,

    /// <summary>Validated. The ONLY status counted in buyer totals (§14.3).</summary>
    Validated = 1,

    /// <summary>Refused. Never counted.</summary>
    Rejected = 2,

    /// <summary>Cancelled by a reversal entry.</summary>
    Reversed = 3
}
