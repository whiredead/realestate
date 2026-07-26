namespace ProjectAPI.Domain.Immeubles.Entities;

/// <summary>
/// Append-only trace of a unit's commercial-status changes (spec §7: every
/// transition writes status history; §6.1: history tables are append-only with
/// an <c>occurred_at</c>).
///
/// Rows are never updated or deleted. The reason a unit is in its current state
/// — which reservation held it, which administrator released it, why — is only
/// answerable from this table.
/// </summary>
public class UnitStatusHistory
{
    public Guid Id { get; set; }

    public Guid UnitId { get; set; }

    /// <summary>
    /// Navigation to the parent unit. Required, and not only for querying: EF Core
    /// infers insert order within a single SaveChangesAsync from navigation
    /// properties rather than bare FK scalars, so without it a history row can be
    /// inserted before the unit it references.
    /// </summary>
    public Unit Unit { get; set; } = null!;

    /// <summary>Null only for the row that records a unit's initial state.</summary>
    public UnitCommercialStatus? FromStatus { get; set; }

    public UnitCommercialStatus ToStatus { get; set; }

    /// <summary>The command that caused the change, e.g. "APPROVE_RESERVATION".</summary>
    public string Cause { get; set; } = string.Empty;

    /// <summary>Reservation the change stemmed from, when there was one.</summary>
    public Guid? ReservationId { get; set; }

    /// <summary>User who triggered it, from the token — not from the request body.</summary>
    public string? ActorUserId { get; set; }

    /// <summary>Mandatory for administrative moves such as SUSPENDED or a forced release (§3).</summary>
    public string? Reason { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
