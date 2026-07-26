namespace ProjectAPI.Domain.Immeubles.Entities;

/// <summary>
/// Commercial status of a unit — spec §3, the backbone of the sale workflow.
///
/// Persisted as the canonical UPPER_SNAKE_CASE code (§6.1: statuses are varchar
/// + CHECK constraint, not database enums). See <see cref="UnitStatusCodes"/>.
/// </summary>
public enum UnitCommercialStatus
{
    /// <summary>Selectable. The only status from which a reservation may start (§3).</summary>
    Available = 0,

    /// <summary>Held by a submitted reservation awaiting an administrative decision.</summary>
    HoldPendingApproval = 1,

    /// <summary>Reservation approved.</summary>
    Reserved = 2,

    /// <summary>
    /// Final contract validated (§3). No command in this codebase produces this
    /// state: the spec names it but never assigns it an owner, a command, a
    /// screen or an endpoint. The value exists so the matrix is complete and so
    /// data created elsewhere round-trips; the notary path goes RESERVED → SOLD
    /// directly, which §47.2 explicitly permits
    /// ("RESERVED ou CONTRACTED | Achat finalisé chez le notaire | SOLD").
    /// Open question for the client — see BACKEND_DOMAIN_AUDIT.md.
    /// </summary>
    Contracted = 3,

    /// <summary>Purchase completed at the notary (§5.7 PURCHASE_COMPLETED).</summary>
    Sold = 4,

    /// <summary>Keys handed over and handover report acknowledged (§5.8). Terminal.</summary>
    Delivered = 5,

    /// <summary>Withdrawn from sale by an administrator, with a reason (§3).</summary>
    Suspended = 6,

    /// <summary>Abandoned. Terminal (§3).</summary>
    Cancelled = 7
}

/// <summary>
/// Canonical string codes for <see cref="UnitCommercialStatus"/>.
///
/// These are the exact §3 codes. They are what the column stores and what the
/// API returns, so the frontend consumes them verbatim (its adapter already
/// accepts canonical codes ahead of the legacy PascalCase spellings).
/// </summary>
public static class UnitStatusCodes
{
    public const string Available = "AVAILABLE";
    public const string HoldPendingApproval = "HOLD_PENDING_APPROVAL";
    public const string Reserved = "RESERVED";
    public const string Contracted = "CONTRACTED";
    public const string Sold = "SOLD";
    public const string Delivered = "DELIVERED";
    public const string Suspended = "SUSPENDED";
    public const string Cancelled = "CANCELLED";

    /// <summary>Every canonical code, in enum order. Drives the CHECK constraint.</summary>
    public static readonly string[] All =
    {
        Available, HoldPendingApproval, Reserved, Contracted,
        Sold, Delivered, Suspended, Cancelled
    };

    public static string ToCode(this UnitCommercialStatus status) => status switch
    {
        UnitCommercialStatus.Available => Available,
        UnitCommercialStatus.HoldPendingApproval => HoldPendingApproval,
        UnitCommercialStatus.Reserved => Reserved,
        UnitCommercialStatus.Contracted => Contracted,
        UnitCommercialStatus.Sold => Sold,
        UnitCommercialStatus.Delivered => Delivered,
        UnitCommercialStatus.Suspended => Suspended,
        UnitCommercialStatus.Cancelled => Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown unit status.")
    };

    /// <summary>
    /// Parses a stored value. Accepts the canonical code and the three legacy
    /// spellings the column carried before this migration ("Available",
    /// "Reserved", "Sold"); anything else is a bug worth surfacing, not
    /// defaulting away.
    /// </summary>
    public static UnitCommercialStatus Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return UnitCommercialStatus.Available;

        return value.Trim().ToUpperInvariant() switch
        {
            Available => UnitCommercialStatus.Available,
            HoldPendingApproval or "HOLDPENDINGAPPROVAL" => UnitCommercialStatus.HoldPendingApproval,
            Reserved => UnitCommercialStatus.Reserved,
            Contracted => UnitCommercialStatus.Contracted,
            Sold => UnitCommercialStatus.Sold,
            Delivered => UnitCommercialStatus.Delivered,
            Suspended => UnitCommercialStatus.Suspended,
            Cancelled => UnitCommercialStatus.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown unit status code.")
        };
    }
}

/// <summary>
/// Authoritative transition matrix for a unit's commercial status (spec §3).
///
/// Mirrors <c>ReservationStateMachine</c>: handlers consult
/// <see cref="EnsureCanTransition"/> rather than assigning
/// <c>unit.Status</c> directly, which is what makes the lifecycle auditable
/// (§7 — direct status updates are forbidden).
///
/// <code>
///   AVAILABLE              → HOLD_PENDING_APPROVAL            (reservation submitted)
///   HOLD_PENDING_APPROVAL  → RESERVED                         (approved)
///   HOLD_PENDING_APPROVAL  → AVAILABLE                        (rejected / expired / cancelled)
///   RESERVED               → CONTRACTED | SOLD                (contract / notary PURCHASE_COMPLETED)
///   CONTRACTED             → SOLD
///   SOLD                   → DELIVERED                        (handover report acknowledged)
///   RESERVED|CONTRACTED|SOLD → AVAILABLE                      (admin cancellation only, §3)
///   any non-DELIVERED      → SUSPENDED | CANCELLED            (admin, with reason)
///   DELIVERED, CANCELLED are terminal.
/// </code>
/// </summary>
public static class UnitStateMachine
{
    private static readonly IReadOnlyDictionary<UnitCommercialStatus, UnitCommercialStatus[]> Allowed =
        new Dictionary<UnitCommercialStatus, UnitCommercialStatus[]>
        {
            [UnitCommercialStatus.Available] = new[]
            {
                UnitCommercialStatus.HoldPendingApproval,
                UnitCommercialStatus.Suspended,
                UnitCommercialStatus.Cancelled
            },

            [UnitCommercialStatus.HoldPendingApproval] = new[]
            {
                UnitCommercialStatus.Reserved,
                UnitCommercialStatus.Available,
                UnitCommercialStatus.Suspended,
                UnitCommercialStatus.Cancelled
            },

            // Returning to AVAILABLE is never automatic (§3): it is reachable here
            // only because an administrator cancels the reservation with a reason
            // and handles the financial cleanup.
            [UnitCommercialStatus.Reserved] = new[]
            {
                UnitCommercialStatus.Contracted,
                UnitCommercialStatus.Sold,
                UnitCommercialStatus.Available,
                UnitCommercialStatus.Suspended,
                UnitCommercialStatus.Cancelled
            },

            [UnitCommercialStatus.Contracted] = new[]
            {
                UnitCommercialStatus.Sold,
                UnitCommercialStatus.Available,
                UnitCommercialStatus.Suspended,
                UnitCommercialStatus.Cancelled
            },

            [UnitCommercialStatus.Sold] = new[]
            {
                UnitCommercialStatus.Delivered,
                UnitCommercialStatus.Available,
                UnitCommercialStatus.Suspended,
                UnitCommercialStatus.Cancelled
            },

            // A suspended unit is put back on sale or abandoned.
            [UnitCommercialStatus.Suspended] = new[]
            {
                UnitCommercialStatus.Available,
                UnitCommercialStatus.Cancelled
            },

            // Terminal (§3): delivery ends the commercial lifecycle, and the doc
            // excludes DELIVERED from the "any non-delivered → SUSPENDED" rule.
            [UnitCommercialStatus.Delivered] = Array.Empty<UnitCommercialStatus>(),
            [UnitCommercialStatus.Cancelled] = Array.Empty<UnitCommercialStatus>()
        };

    /// <summary>Statuses in which a unit is still selectable for a new reservation.</summary>
    public static bool IsSelectable(UnitCommercialStatus status) =>
        status == UnitCommercialStatus.Available;

    public static bool CanTransition(UnitCommercialStatus from, UnitCommercialStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static bool IsTerminal(UnitCommercialStatus status) =>
        Allowed.TryGetValue(status, out var targets) && targets.Length == 0;

    /// <summary>
    /// Throws <see cref="InvalidUnitTransitionException"/> when the move is not
    /// in the matrix. Translated to 409 INVALID_STATUS_TRANSITION by the API layer.
    /// </summary>
    public static void EnsureCanTransition(UnitCommercialStatus from, UnitCommercialStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidUnitTransitionException(from, to);
        }
    }
}

/// <summary>
/// Raised when a handler attempts a unit transition the matrix forbids.
/// Translated to <c>409 INVALID_STATUS_TRANSITION</c> by the API layer.
/// </summary>
public class InvalidUnitTransitionException : Exception
{
    public UnitCommercialStatus From { get; }
    public UnitCommercialStatus To { get; }

    public InvalidUnitTransitionException(UnitCommercialStatus from, UnitCommercialStatus to)
        : base($"Transition de bien non autorisée : {from.ToCode()} → {to.ToCode()}.")
    {
        From = from;
        To = to;
    }
}
