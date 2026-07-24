namespace ProjectAPI.Domain.Reservations.Entities;

/// <summary>
/// Authoritative transition matrix for a reservation (spec §12.4).
///
/// Handlers MUST consult <see cref="EnsureCanTransition"/> rather than writing
/// their own status checks: keeping the rules in one place is what makes the
/// lifecycle auditable and testable.
///
/// Allowed transitions:
/// <code>
///   DRAFT             → SUBMITTED
///   SUBMITTED         → CHANGES_REQUESTED | APPROVED | REJECTED | EXPIRED
///   CHANGES_REQUESTED → SUBMITTED | REJECTED | EXPIRED
///   APPROVED          → CONVERTED | CANCELLED
///   REJECTED, EXPIRED, CANCELLED, CONVERTED are terminal.
/// </code>
/// </summary>
public static class ReservationStateMachine
{
    private static readonly IReadOnlyDictionary<ReservationStatus, ReservationStatus[]> Allowed =
        new Dictionary<ReservationStatus, ReservationStatus[]>
        {
            [ReservationStatus.Draft] = new[]
            {
                ReservationStatus.Pending
            },

            // Pending == spec SUBMITTED
            [ReservationStatus.Pending] = new[]
            {
                ReservationStatus.ChangesRequested,
                ReservationStatus.Approved,
                ReservationStatus.Rejected,
                ReservationStatus.Expired
            },

            [ReservationStatus.ChangesRequested] = new[]
            {
                ReservationStatus.Pending,
                ReservationStatus.Rejected,
                ReservationStatus.Expired
            },

            // Sold == spec CONVERTED
            [ReservationStatus.Approved] = new[]
            {
                ReservationStatus.Sold,
                ReservationStatus.Cancelled
            },

            // Terminal states
            [ReservationStatus.Rejected] = Array.Empty<ReservationStatus>(),
            [ReservationStatus.Expired] = Array.Empty<ReservationStatus>(),
            [ReservationStatus.Cancelled] = Array.Empty<ReservationStatus>(),
            [ReservationStatus.Sold] = Array.Empty<ReservationStatus>()
        };

    /// <summary>
    /// Statuses that block the unit, i.e. count as an "active" reservation for
    /// the one-active-reservation-per-unit rule.
    ///
    /// The spec fixes this set exactly (§7.7 / §30.4 / §48.5):
    /// SUBMITTED, CHANGES_REQUESTED, APPROVED and CONVERTED.
    /// CONVERTED (<see cref="ReservationStatus.Sold"/>) is included: a sold unit
    /// must never be reservable again.
    ///
    /// DRAFT is deliberately excluded — a draft must not block a unit (§12.1).
    ///
    /// This set MUST stay in sync with the filtered index
    /// <c>IX_Reservations_ActivePerUnit</c> (Status IN (0, 1, 4, 6)).
    /// </summary>
    public static readonly ReservationStatus[] UnitBlockingStatuses =
    {
        ReservationStatus.Pending,          // SUBMITTED
        ReservationStatus.ChangesRequested, // CHANGES_REQUESTED
        ReservationStatus.Approved,         // APPROVED
        ReservationStatus.Sold              // CONVERTED
    };

    /// <summary>Returns true when the transition is permitted.</summary>
    public static bool CanTransition(ReservationStatus from, ReservationStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>Returns true when no transition can leave this status.</summary>
    public static bool IsTerminal(ReservationStatus status) =>
        Allowed.TryGetValue(status, out var targets) && targets.Length == 0;

    /// <summary>True when the status blocks its unit from being reserved again.</summary>
    public static bool BlocksUnit(ReservationStatus status) =>
        UnitBlockingStatuses.Contains(status);

    /// <summary>
    /// Throws <see cref="InvalidReservationTransitionException"/> when the
    /// transition is not allowed.
    /// </summary>
    public static void EnsureCanTransition(ReservationStatus from, ReservationStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidReservationTransitionException(from, to);
        }
    }
}

/// <summary>
/// Raised when a handler attempts a transition the matrix forbids.
/// Translated to <c>409 INVALID_STATUS_TRANSITION</c> by the API layer.
/// </summary>
public class InvalidReservationTransitionException : Exception
{
    public ReservationStatus From { get; }
    public ReservationStatus To { get; }

    public InvalidReservationTransitionException(ReservationStatus from, ReservationStatus to)
        : base($"Transition non autorisée : {from} → {to}.")
    {
        From = from;
        To = to;
    }
}
