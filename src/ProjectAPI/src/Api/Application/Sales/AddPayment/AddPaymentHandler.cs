using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Payments;
using ProjectAPI.Domain.Payments.Entities;
using ProjectAPI.Domain.Purchases.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Sales.AddPayment;

/// <summary>
/// Legacy endpoint <c>POST /api/sales/{saleId}/payments</c>, kept for API
/// compatibility but now writing into the payment ledger (spec §14.3).
///
/// It previously inserted a <c>PaymentTracking</c> row and did
/// <c>purchase.PaidAmount += amount</c>. That stored total could drift from
/// reality and, once wrong, stayed wrong — the failure mode that produced
/// anomaly A20. Payments are now recorded as immutable ledger entries and the
/// cached totals are RECOMPUTED, never incremented.
///
/// New integrations should call <c>POST /api/payments/reservations/{id}/payments</c>,
/// which exposes allocation and validation control.
/// </summary>
public class AddPaymentHandler : IRequestHandler<AddPaymentCommand, bool>
{
    private readonly ApplicationDbContext _db;
    private readonly PurchaseTotalsService _purchaseTotals;

    public AddPaymentHandler(ApplicationDbContext db, PurchaseTotalsService purchaseTotals)
    {
        _db = db;
        _purchaseTotals = purchaseTotals;
    }

    public async Task<bool> Handle(AddPaymentCommand r, CancellationToken ct)
    {
        if (r.AmountPaid <= 0)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "Le montant d'un paiement doit être strictement positif.");
        }

        var sale = await _db.Set<Domain.Sales.Entities.Sale>()
            .FirstOrDefaultAsync(s => s.Id == r.SaleId, ct)
            ?? throw new NotFoundException($"Sale {r.SaleId} not found.");

        // The ledger is keyed by reservation, so resolve it through the Purchase
        // that links sale and reservation together.
        var purchase = await _db.Set<Purchase>()
            .FirstOrDefaultAsync(p => p.SaleId == r.SaleId, ct);

        var reservationId = purchase?.ReservationId
            ?? throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                $"La vente {r.SaleId} n'est rattachée à aucune réservation : impossible d'enregistrer le paiement.");

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            ReservationId = reservationId,
            PaymentDate = r.PaymentDate == default ? DateTime.UtcNow : r.PaymentDate,
            Amount = r.AmountPaid,
            Currency = "MAD",
            MethodCode = null,
            Source = "WEB",
            // This legacy route carries no validation workflow, so entries are
            // recorded as already validated, matching its previous behaviour.
            Status = PaymentStatus.Validated,
            ValidatedAt = DateTime.UtcNow,
            Comment = string.IsNullOrWhiteSpace(r.Status) ? null : $"Statut déclaré : {r.Status}",
            CreatedAt = DateTime.UtcNow
        };

        _db.Add(payment);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        await _db.SaveChangesAsync(ct);
        await _purchaseTotals.RefreshForReservationAsync(reservationId, ct);
        await _db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);

        return true;
    }
}
