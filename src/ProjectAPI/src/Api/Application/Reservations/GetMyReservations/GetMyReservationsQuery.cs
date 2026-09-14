using ProjectAPI.Api.Application.Common.Units;
namespace ProjectAPI.Api.Application.Reservations.GetMyReservations;

/// <summary>
/// §8 "Prospect/Buyer — My properties" — the buyer portal's entry point when a
/// buyer holds more than one file. Deliberately takes NO buyer/user id from the
/// caller: it always resolves to ICurrentUser.UserId, so there is no way to
/// pass someone else's id and see their reservations (unlike
/// GetReservationsQuery, which is an internal-staff tool that trusts a
/// client-supplied BuyerId filter and is gated accordingly).
/// </summary>
public class GetMyReservationsQuery : IRequest<List<MyReservationSummary>>
{
}

public class MyReservationSummary : IHasUnitLocation
{
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public Guid? ImmeubleId { get; set; }
    public string? ImmeubleName { get; set; }
    public string? FloorName { get; set; }
    public string? UnitNumber { get; set; }
    public UnitContextDto? UnitContext { get; set; }

    public Guid Id { get; set; }
    public Guid UnitId { get; set; }
    public string? UnitDetails { get; set; }

    /// <summary>
    /// Numeric wire value of ReservationStatus, matching every other
    /// reservation endpoint (see ReservationConfiguration.HasConversion&lt;int&gt;
    /// and the frontend's reservationStatus.fromInt table in enums.ts).
    /// </summary>
    public int Status { get; set; }

    public decimal TotalPropertyPrice { get; set; }
    public decimal? FinalPrice { get; set; }
    public DateTime ReservationDate { get; set; }
    public DateTime? ValidatedAt { get; set; }
}
