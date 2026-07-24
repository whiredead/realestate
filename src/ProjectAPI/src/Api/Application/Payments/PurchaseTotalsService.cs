using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Payments.Entities;
using ProjectAPI.Domain.Purchases.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Payments;

/// <summary>
/// Keeps <see cref="Purchase.PaidAmount"/> / <see cref="Purchase.RemainingAmount"/>
/// in step with the payment ledger.
///
/// Those two columns predate the ledger and are still read by
/// <c>GetSalesByUser</c> and <c>GetUserPurchases</c> (and therefore by the
/// frontend), so they are kept — but they are now a **derived cache**, never a
/// value edited in place.
///
/// The distinction matters. The old code did <c>PaidAmount += amount</c>: any
/// bug wrote a wrong number that stayed wrong forever. Here the value is always
/// RECOMPUTED from the ledger (§5.8), so it is self-correcting and a fix to the
/// calculation repairs every row on the next write.
///
/// The ledger is the source of truth; this is only a read optimisation.
/// </summary>
public class PurchaseTotalsService
{
    private readonly ApplicationDbContext _db;

    public PurchaseTotalsService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Recomputes the cached totals of every purchase attached to a reservation.
    /// Call after any ledger change. Does NOT save — the caller commits, so the
    /// ledger write and the cache refresh land in the same transaction (§5.7).
    /// </summary>
    public async Task RefreshForReservationAsync(Guid reservationId, CancellationToken ct)
    {
        var purchases = await _db.Set<Purchase>()
            .Where(p => p.ReservationId == reservationId)
            .ToListAsync(ct);

        if (purchases.Count == 0) return;

        var payments = await _db.Set<Payment>()
            .Where(p => p.ReservationId == reservationId)
            .ToListAsync(ct);

        // Same rule as everywhere else: validated entries plus reversals, which
        // carry a negative amount and cancel the entry they reverse.
        var paid = PaymentCalculator.TotalValidated(payments);

        foreach (var purchase in purchases)
        {
            purchase.PaidAmount = paid;
            purchase.RemainingAmount = purchase.TotalPrice - paid;
        }
    }
}
