namespace ProjectAPI.Domain.Payments.Entities;

/// <summary>
/// Payment schedule (échéancier) attached to a reservation — spec §14.2, §48.6.
///
/// A schedule is versioned: activating a replacement supersedes the previous one
/// rather than rewriting it, so already-paid installments keep their history.
/// At most ONE schedule per reservation may be <see cref="PaymentScheduleStatus.Active"/>.
/// </summary>
public class PaymentSchedule
{
    public Guid Id { get; set; }

    public Guid ReservationId { get; set; }

    /// <summary>1-based version; increments each time a schedule supersedes another.</summary>
    public int VersionNo { get; set; } = 1;

    public PaymentScheduleStatus Status { get; set; } = PaymentScheduleStatus.Draft;

    /// <summary>Contractual amount the installments must add up to (§14.2).</summary>
    public decimal ContractAmount { get; set; }

    /// <summary>ISO 4217 code. Defaults to the functional currency (§5.6).</summary>
    public string Currency { get; set; } = "MAD";

    public DateTime? ActivatedAt { get; set; }

    /// <summary>Schedule this one replaces, when it was created as a revision.</summary>
    public Guid? SupersedesId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PaymentInstallment> Installments { get; set; } = new List<PaymentInstallment>();
}

/// <summary>Lifecycle of a payment schedule (§14.2).</summary>
public enum PaymentScheduleStatus
{
    /// <summary>Being prepared; percentages may still be incomplete.</summary>
    Draft = 0,

    /// <summary>In force. Only one per reservation.</summary>
    Active = 1,

    /// <summary>Replaced by a newer version.</summary>
    Superseded = 2,

    /// <summary>Abandoned administratively.</summary>
    Cancelled = 3
}
