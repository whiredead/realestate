namespace ProjectAPI.Domain.Payments.Entities;

/// <summary>
/// One call for funds (appel de fonds) within a schedule — spec §14.2, §48.6.
///
/// <see cref="PaymentInstallmentStatus"/> is DERIVED (§14.3 FR-PAY-004): it is
/// computed from the due date and the validated allocations, never stored as an
/// independent editable value.
/// </summary>
public class PaymentInstallment
{
    public Guid Id { get; set; }

    public Guid ScheduleId { get; set; }
    public PaymentSchedule Schedule { get; set; } = null!;

    /// <summary>Order within the schedule. Unique per schedule.</summary>
    public int SequenceNo { get; set; }

    public string LabelFr { get; set; } = string.Empty;
    public string LabelEn { get; set; } = string.Empty;

    /// <summary>Share of the contract amount, numeric(7,4) per §30.1.</summary>
    public decimal Percentage { get; set; }

    /// <summary>Amount due, numeric(15,2) per §30.1.</summary>
    public decimal Amount { get; set; }

    public DateTime DueDate { get; set; }

    /// <summary>Set when an installment is voided administratively (§14.3).</summary>
    public bool IsCancelled { get; set; }

    public string? Comment { get; set; }

    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
}

/// <summary>Derived installment status (§14.3 FR-PAY-004).</summary>
public enum PaymentInstallmentStatus
{
    /// <summary>Due date in the future, not fully covered.</summary>
    Upcoming = 0,

    /// <summary>Due date reached.</summary>
    Due = 1,

    /// <summary>Partially covered by validated payments.</summary>
    PartiallyPaid = 2,

    /// <summary>Fully covered.</summary>
    Paid = 3,

    /// <summary>Past due with an outstanding balance.</summary>
    Overdue = 4,

    /// <summary>Cancelled administratively.</summary>
    Cancelled = 5
}
