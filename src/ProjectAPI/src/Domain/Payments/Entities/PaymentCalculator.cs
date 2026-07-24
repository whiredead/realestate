namespace ProjectAPI.Domain.Payments.Entities;

/// <summary>
/// Derives installment statuses and buyer totals from source entries
/// (spec §5.8, §14.3).
///
/// Financial figures are NEVER stored as independent values: they are computed
/// from validated payments so that a stale cache can never contradict the ledger.
/// Only <see cref="PaymentStatus.Validated"/> entries count; pending and rejected
/// ones do not move buyer totals (§14.3 FR-PAY-005).
/// </summary>
public static class PaymentCalculator
{
    /// <summary>Rounding tolerance allowed by the spec on amounts (§14.2).</summary>
    public const decimal AmountTolerance = 0.01m;

    /// <summary>Rounding tolerance allowed on percentage sums (§14.2).</summary>
    public const decimal PercentageTolerance = 0.01m;

    /// <summary>
    /// True when an entry contributes to financial totals.
    ///
    /// Both VALIDATED and REVERSED entries count: a reversed payment really was
    /// received and validated, and its cancellation is carried by the separate
    /// negative reversal entry. Excluding the original as well would cancel it
    /// twice and drive totals negative.
    /// </summary>
    public static bool CountsTowardTotals(Payment payment) =>
        payment.Status is PaymentStatus.Validated or PaymentStatus.Reversed;

    /// <summary>
    /// Amount actually settled on an installment: the sum of allocations that
    /// belong to counted payments. Reversals carry a negative amount and are
    /// therefore subtracted naturally.
    /// </summary>
    public static decimal PaidAmount(PaymentInstallment installment, IEnumerable<Payment> payments)
    {
        var paymentsById = payments.ToDictionary(p => p.Id);

        decimal total = 0m;

        foreach (var allocation in installment.Allocations)
        {
            if (!paymentsById.TryGetValue(allocation.PaymentId, out var payment)) continue;
            if (!CountsTowardTotals(payment)) continue;
            total += allocation.AllocatedAmount;
        }

        // A reversal cancels the allocations of the payment it reverses, even
        // though the reversal itself carries no allocation rows.
        var reversedPaymentIds = payments
            .Where(p => p.ReversalOfPaymentId is not null && CountsTowardTotals(p))
            .Select(p => p.ReversalOfPaymentId!.Value)
            .ToHashSet();

        foreach (var allocation in installment.Allocations)
        {
            if (reversedPaymentIds.Contains(allocation.PaymentId))
            {
                total -= allocation.AllocatedAmount;
            }
        }

        return total;
    }

    /// <summary>Computes the derived status of an installment (§14.3 FR-PAY-004).</summary>
    public static PaymentInstallmentStatus StatusOf(
        PaymentInstallment installment,
        IEnumerable<Payment> payments,
        DateTime asOfUtc)
    {
        if (installment.IsCancelled)
        {
            return PaymentInstallmentStatus.Cancelled;
        }

        var paid = PaidAmount(installment, payments);
        var isDue = installment.DueDate.Date <= asOfUtc.Date;

        // Fully settled (tolerance covers rounding on percentage-derived amounts).
        if (paid >= installment.Amount - AmountTolerance)
        {
            return PaymentInstallmentStatus.Paid;
        }

        if (paid > 0)
        {
            // A partially paid installment past its due date is still overdue:
            // the outstanding balance is what matters operationally.
            return isDue ? PaymentInstallmentStatus.Overdue : PaymentInstallmentStatus.PartiallyPaid;
        }

        if (!isDue)
        {
            return PaymentInstallmentStatus.Upcoming;
        }

        // Due today counts as DUE; strictly past due counts as OVERDUE.
        return installment.DueDate.Date == asOfUtc.Date
            ? PaymentInstallmentStatus.Due
            : PaymentInstallmentStatus.Overdue;
    }

    /// <summary>
    /// Net amount received for a reservation. Reversals are stored as negative
    /// entries, so summing the counted entries yields the correct net figure:
    /// +300000 (reversed original) and -300000 (its reversal) cancel to zero.
    /// PENDING_VALIDATION and REJECTED entries never contribute (§14.3).
    /// </summary>
    public static decimal TotalValidated(IEnumerable<Payment> payments) =>
        payments.Where(CountsTowardTotals).Sum(p => p.Amount);

    /// <summary>Total called for by the active schedule, ignoring cancelled lines.</summary>
    public static decimal TotalCalled(PaymentSchedule schedule) =>
        schedule.Installments.Where(i => !i.IsCancelled).Sum(i => i.Amount);

    /// <summary>
    /// True when the schedule may be activated: percentages sum to 100 and
    /// amounts match the contract amount, both within tolerance (§14.2 FR-PAY-002).
    /// </summary>
    public static bool CanActivate(PaymentSchedule schedule, out string? reason)
    {
        var active = schedule.Installments.Where(i => !i.IsCancelled).ToList();

        if (active.Count == 0)
        {
            reason = "L'échéancier ne contient aucune échéance.";
            return false;
        }

        var percentSum = active.Sum(i => i.Percentage);
        if (Math.Abs(percentSum - 100m) > PercentageTolerance)
        {
            reason = $"La somme des pourcentages doit être égale à 100 % (actuellement {percentSum:N2} %).";
            return false;
        }

        var amountSum = active.Sum(i => i.Amount);
        if (Math.Abs(amountSum - schedule.ContractAmount) > AmountTolerance)
        {
            reason = $"La somme des montants ({amountSum:N2}) doit correspondre au prix contractuel ({schedule.ContractAmount:N2}).";
            return false;
        }

        reason = null;
        return true;
    }
}
