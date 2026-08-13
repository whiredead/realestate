using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Notifications;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Payments.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Payments.RecordPayment;

/// <summary>
/// Records an incoming payment and allocates it against the active schedule
/// (spec §14.3).
///
/// Key rules enforced here:
///   - amount must be strictly positive (reversals use ReversePayment);
///   - the declared method must be a known code;
///   - allocations may not exceed the payment;
///   - a surplus becomes an explicit unallocated credit, never silently absorbed.
/// </summary>
public class RecordPaymentHandler : IRequestHandler<RecordPaymentCommand, RecordPaymentResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly PurchaseTotalsService _purchaseTotals;
    private readonly ProjectScopeService _projectScope;
    private readonly INotificationService _notifications;

    public RecordPaymentHandler(
        ApplicationDbContext db,
        PurchaseTotalsService purchaseTotals,
        ProjectScopeService projectScope,
        INotificationService notifications)
    {
        _db = db;
        _purchaseTotals = purchaseTotals;
        _projectScope = projectScope;
        _notifications = notifications;
    }

    public async Task<RecordPaymentResponse> Handle(RecordPaymentCommand request, CancellationToken ct)
    {
        // §6.4 — recording a payment is admin-only and project-scoped.
        await _projectScope.EnsureReservationAccessAsync(request.ReservationId, ct);

        if (request.Amount <= 0)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "Le montant d'un paiement doit être strictement positif. Utilisez une contrepassation pour annuler.");
        }

        if (request.MethodCode is not null && !PaymentMethodCodes.IsValid(request.MethodCode))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                $"Mode de paiement inconnu : {request.MethodCode}.");
        }

        var reservation = await _db.Set<Reservation>().FirstOrDefaultAsync(r => r.Id == request.ReservationId, ct)
            ?? throw new NotFoundException($"Reservation {request.ReservationId} not found.");

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            ReservationId = request.ReservationId,
            PaymentDate = request.PaymentDate == default ? DateTime.UtcNow : request.PaymentDate,
            Amount = request.Amount,
            Currency = "MAD",
            MethodCode = request.MethodCode,
            ExternalReference = string.IsNullOrWhiteSpace(request.ExternalReference) ? null : request.ExternalReference,
            Source = "WEB",
            Status = request.ValidateImmediately ? PaymentStatus.Validated : PaymentStatus.PendingValidation,
            Comment = request.Comment,
            CreatedBy = request.CreatedBy,
            CreatedAt = DateTime.UtcNow
        };

        if (payment.Status == PaymentStatus.Validated)
        {
            payment.ValidatedBy = request.CreatedBy;
            payment.ValidatedAt = DateTime.UtcNow;
        }

        var allocations = await BuildAllocationsAsync(request, payment, ct);
        foreach (var allocation in allocations)
        {
            _db.Add(allocation);
        }

        _db.Add(payment);

        // One transaction covering both writes: the ledger entry and the refresh
        // of the legacy cached totals must commit together, or neither (§5.7).
        // The entry is saved first because RefreshForReservationAsync queries the
        // database and would otherwise not see it.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        await _db.SaveChangesAsync(ct);
        await _purchaseTotals.RefreshForReservationAsync(request.ReservationId, ct);
        await _db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);

        // §6.2 — non-fatal: the payment entry is already durable.
        if (!string.IsNullOrWhiteSpace(reservation.BuyerId))
        {
            await _notifications.NotifyAsync(
                reservation.BuyerId, "PAYMENT_RECORDED",
                "Paiement enregistré",
                $"Un paiement de {request.Amount:N2} MAD a été enregistré sur votre dossier.",
                payment.Id, "Payment", ct);
        }

        var allocated = allocations.Where(a => a.InstallmentId != null).Sum(a => a.AllocatedAmount);
        var unallocated = allocations.Where(a => a.InstallmentId == null).Sum(a => a.AllocatedAmount);

        return new RecordPaymentResponse
        {
            PaymentId = payment.Id,
            Status = payment.Status.ToString(),
            AllocatedAmount = allocated,
            UnallocatedAmount = unallocated,
            Message = unallocated > 0
                ? $"Paiement enregistré. {unallocated:N2} MAD non affectés (crédit à répartir)."
                : "Paiement enregistré."
        };
    }

    private async Task<List<PaymentAllocation>> BuildAllocationsAsync(
        RecordPaymentCommand request,
        Payment payment,
        CancellationToken ct)
    {
        var result = new List<PaymentAllocation>();

        // --- explicit split supplied by the caller ---
        if (request.Allocations is { Count: > 0 })
        {
            var total = request.Allocations.Sum(a => a.Amount);
            if (total > payment.Amount + PaymentCalculator.AmountTolerance)
            {
                throw new BusinessRuleException(
                    BusinessErrorCodes.ValidationFailed,
                    $"La somme des affectations ({total:N2}) dépasse le montant du paiement ({payment.Amount:N2}).");
            }

            foreach (var input in request.Allocations)
            {
                var exists = await _db.Set<PaymentInstallment>().AnyAsync(i => i.Id == input.InstallmentId, ct);
                if (!exists)
                {
                    throw new NotFoundException($"Installment {input.InstallmentId} not found.");
                }

                result.Add(new PaymentAllocation
                {
                    Id = Guid.NewGuid(),
                    PaymentId = payment.Id,
                    InstallmentId = input.InstallmentId,
                    AllocatedAmount = input.Amount
                });
            }

            var remainder = payment.Amount - total;
            if (remainder > PaymentCalculator.AmountTolerance)
            {
                result.Add(UnallocatedCredit(payment.Id, remainder));
            }

            return result;
        }

        // --- automatic allocation: oldest unpaid installment first ---
        var schedule = await _db.Set<PaymentSchedule>()
            .Include(s => s.Installments)
                .ThenInclude(i => i.Allocations)
            .FirstOrDefaultAsync(
                s => s.ReservationId == request.ReservationId && s.Status == PaymentScheduleStatus.Active,
                ct);

        if (schedule is null)
        {
            // No active schedule: the money is real but cannot be applied yet.
            result.Add(UnallocatedCredit(payment.Id, payment.Amount));
            return result;
        }

        // Load every entry: PaymentCalculator decides which ones count, so a
        // reversed payment correctly frees the amount it had settled.
        var validatedPayments = await _db.Set<Payment>()
            .Where(p => p.ReservationId == request.ReservationId)
            .ToListAsync(ct);

        var remaining = payment.Amount;

        foreach (var installment in schedule.Installments
                     .Where(i => !i.IsCancelled)
                     .OrderBy(i => i.SequenceNo))
        {
            if (remaining <= PaymentCalculator.AmountTolerance) break;

            var alreadyPaid = PaymentCalculator.PaidAmount(installment, validatedPayments);
            var outstanding = installment.Amount - alreadyPaid;
            if (outstanding <= PaymentCalculator.AmountTolerance) continue;

            var applied = Math.Min(remaining, outstanding);

            result.Add(new PaymentAllocation
            {
                Id = Guid.NewGuid(),
                PaymentId = payment.Id,
                InstallmentId = installment.Id,
                AllocatedAmount = applied
            });

            remaining -= applied;
        }

        if (remaining > PaymentCalculator.AmountTolerance)
        {
            result.Add(UnallocatedCredit(payment.Id, remaining));
        }

        return result;
    }

    private static PaymentAllocation UnallocatedCredit(Guid paymentId, decimal amount) => new()
    {
        Id = Guid.NewGuid(),
        PaymentId = paymentId,
        InstallmentId = null,
        AllocatedAmount = amount
    };
}
