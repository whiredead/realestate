namespace ProjectAPI.Domain.Payments.Entities;

/// <summary>
/// Links a payment to the installment it settles — spec §14.3, §48.6.
///
/// A payment may be split across several installments, and an amount exceeding
/// the installment balance must be allocated explicitly rather than silently
/// absorbed (§14.3). An allocation with a null <see cref="InstallmentId"/>
/// represents an unallocated credit.
/// </summary>
public class PaymentAllocation
{
    public Guid Id { get; set; }

    public Guid PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;

    /// <summary>Target installment; null means an unallocated credit.</summary>
    public Guid? InstallmentId { get; set; }
    public PaymentInstallment? Installment { get; set; }

    /// <summary>Portion of the payment applied here, numeric(15,2).</summary>
    public decimal AllocatedAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
