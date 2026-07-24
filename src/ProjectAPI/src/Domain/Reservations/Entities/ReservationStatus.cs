namespace ProjectAPI.Domain.Reservations.Entities;

/// <summary>
/// Reservation lifecycle (spec §12.4).
///
/// IMPORTANT — the numeric values 0..4 are already persisted in the database and
/// are part of the API contract. They MUST NOT be renumbered. New states are
/// appended with fresh values.
///
/// Spec name mapping: <see cref="Pending"/> is the spec's SUBMITTED and
/// <see cref="Sold"/> its CONVERTED. The historical names are kept to avoid a
/// breaking rename across the API, the seed data and the frontend.
/// </summary>
public enum ReservationStatus
{
    /// <summary>Spec `SUBMITTED` — submitted, awaiting administrative decision.</summary>
    Pending = 0,

    /// <summary>Spec `APPROVED` — approved; the unit is reserved.</summary>
    Approved = 1,

    /// <summary>Spec `REJECTED` — refused by the administrator. Terminal.</summary>
    Rejected = 2,

    /// <summary>Spec `CANCELLED` — cancelled after approval. Terminal.</summary>
    Cancelled = 3,

    /// <summary>Spec `CONVERTED` — converted into a sale. Terminal.</summary>
    Sold = 4,

    /// <summary>Spec `DRAFT` — agent draft. Does NOT block the unit (§12.1).</summary>
    Draft = 5,

    /// <summary>Spec `CHANGES_REQUESTED` — correction requested; unit stays blocked (§12.2).</summary>
    ChangesRequested = 6,

    /// <summary>Spec `EXPIRED` — expired at ExpiresAt. Terminal; releases the unit (§12.3).</summary>
    Expired = 7
}
