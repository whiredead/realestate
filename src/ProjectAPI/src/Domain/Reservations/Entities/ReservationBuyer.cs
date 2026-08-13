using ProjectAPI.Domain.Crm.Entities;

namespace ProjectAPI.Domain.Reservations.Entities;

/// <summary>
/// A co-buyer on a reservation (spec §5.3/§6.2 <c>reservation_buyers</c>).
///
/// The primary buyer's identity lives on <see cref="Reservation.PrimaryContactId"/>
/// and is not duplicated here — this table only holds the ADDITIONAL people on
/// the file. Each co-buyer is a distinct <see cref="CrmContact"/> (never a
/// second contact for someone already known, per §1.1's ContactResolver), with
/// their own ownership percentage. When any percentages are set on a file,
/// primary + all co-buyers here must total 100% (±0.01 pt).
/// </summary>
public class ReservationBuyer
{
    public Guid Id { get; set; }

    public Guid ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;

    public Guid CrmContactId { get; set; }
    public CrmContact CrmContact { get; set; } = null!;

    /// <summary>Share of ownership, 0-100. Null when the file does not track percentages.</summary>
    public decimal? OwnershipPercent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
