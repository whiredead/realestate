using ProjectAPI.Domain.Immeubles.Entities;

namespace ProjectAPI.Domain.Sales.Entities;

/// <summary>
/// Sale lifecycle (§5.7 / §6).
///
/// Persisted as int; values MUST NOT be renumbered — the filtered unique
/// indexes IX_Sales_ActivePerReservation / IX_Sales_ActivePerUnit are written
/// against the stored numbers (0, 1, 2).
/// </summary>
public enum SaleStatus
{
    /// <summary>Created manually by an agent/admin. Editable. Blocks the unit.</summary>
    Draft = 0,

    /// <summary>Awaiting the notarial act. Editable. Blocks the unit.</summary>
    PendingNotary = 1,

    /// <summary>Finalised at the notary (PURCHASE_COMPLETED). Read-only.</summary>
    Confirmed = 2,

    /// <summary>Abandoned before confirmation. Terminal, releases the unit.</summary>
    Cancelled = 3
}

/// <summary>Transition rules for <see cref="SaleStatus"/>.</summary>
public static class SaleStateMachine
{
    private static readonly IReadOnlyDictionary<SaleStatus, SaleStatus[]> Allowed =
        new Dictionary<SaleStatus, SaleStatus[]>
        {
            [SaleStatus.Draft] = new[] { SaleStatus.PendingNotary, SaleStatus.Confirmed, SaleStatus.Cancelled },
            // Back to Draft when the notary appointment falls through (cancelled,
            // rejected, no-show, or completed without PURCHASE_COMPLETED).
            [SaleStatus.PendingNotary] = new[] { SaleStatus.Confirmed, SaleStatus.Cancelled, SaleStatus.Draft },
            [SaleStatus.Confirmed] = Array.Empty<SaleStatus>(),
            [SaleStatus.Cancelled] = Array.Empty<SaleStatus>()
        };

    /// <summary>
    /// Statuses that occupy a reservation and a unit, i.e. count as an "active"
    /// sale. MUST stay in sync with the filtered unique indexes
    /// <c>IX_Sales_ActivePerReservation</c> and <c>IX_Sales_ActivePerUnit</c>,
    /// whose filter is <c>[Status] IN (0, 1, 2)</c>.
    /// </summary>
    public static readonly SaleStatus[] ActiveStatuses =
    {
        SaleStatus.Draft, SaleStatus.PendingNotary, SaleStatus.Confirmed
    };

    /// <summary>True while the sale's business fields may still be edited (§6).</summary>
    public static bool IsEditable(SaleStatus status) =>
        status is SaleStatus.Draft or SaleStatus.PendingNotary;

    public static bool IsActive(SaleStatus status) => ActiveStatuses.Contains(status);

    public static bool CanTransition(SaleStatus from, SaleStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static void EnsureCanTransition(SaleStatus from, SaleStatus to)
    {
        if (from != to && !CanTransition(from, to))
        {
            throw new InvalidSaleTransitionException(from, to);
        }
    }
}

/// <summary>Raised when a handler attempts a sale transition the matrix forbids.</summary>
public class InvalidSaleTransitionException : Exception
{
    public SaleStatus From { get; }
    public SaleStatus To { get; }

    public InvalidSaleTransitionException(SaleStatus from, SaleStatus to)
        : base($"Transition de vente non autorisée : {from} → {to}.")
    {
        From = from;
        To = to;
    }
}

/// <summary>
/// Represents a sale made by an agent to a user.
/// </summary>
public class Sale
{
    public Guid Id { get; set; }

    // Optional user in your system
    public string? BuyerId { get; set; }

    // Required snapshot (always filled either from BuyerId or manual entry)
    public string BuyerFirstName { get; set; } = null!;
    public string BuyerLastName { get; set; } = null!;
    public string BuyerEmail { get; set; } = null!;
    public string BuyerPhoneNumber { get; set; } = null!;
    public string? BuyerCIN { get; set; }

    public Guid UnitId { get; set; }
    public Unit Unit { get; set; }
    public DateTime SaleDate { get; set; }
    public decimal TotalPrice { get; set; }
    public bool IsUnderConstruction { get; set; }

    /// <summary>
    /// The reservation this sale realises (§6).
    ///
    /// Nullable only so historic rows survive the migration: those predate the
    /// column and are back-filled by UnitId where exactly one reservation
    /// matches, left null where ambiguous rather than guessed. Every sale
    /// created from now on carries it — it is what makes "one active sale per
    /// reservation" enforceable, and the auto-confirmation path keys on it.
    /// </summary>
    public Guid? ReservationId { get; set; }

    /// <summary>
    /// Lifecycle position. Existing rows are back-filled to
    /// <see cref="SaleStatus.Confirmed"/>: they only ever existed because the
    /// notary recorded PURCHASE_COMPLETED.
    /// </summary>
    public SaleStatus Status { get; set; } = SaleStatus.Draft;

    /// <summary>
    /// Warranty length in months, copied from the project at creation and frozen
    /// when the sale is confirmed (§8). The handover reads this rather than
    /// accepting a caller-supplied figure.
    /// </summary>
    public int WarrantyMonths { get; set; }

    /// <summary>§5.3 — agreed price after discount, snapshot from the reservation.</summary>
    public decimal? FinalPrice { get; set; }

    /// <summary>Deposit already paid on the reservation, snapshot at creation.</summary>
    public decimal? ReservationAmount { get; set; }

    /// <summary>FinalPrice − ReservationAmount at creation; not recomputed from the ledger.</summary>
    public decimal? RemainingAmount { get; set; }

    public string? Notes { get; set; }

    /// <summary>Who created the sale. Always taken from the token, never the request body.</summary>
    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set once, when the notary outcome confirms the sale.</summary>
    public DateTime? ConfirmedAt { get; set; }

    public ICollection<PaymentTracking> Payments { get; set; } = [];
    public ICollection<PropertyDelivery> PropertyDeliveries { get; set; } = [];
}
