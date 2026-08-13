namespace ProjectAPI.Api.Application.Handovers.GetMyWarranties;

/// <summary>
/// §5.9/§20/§8 — warranty coverage for one of the buyer's own reservations.
/// Ownership proven through Reservation.BuyerId, same as GetMyHandoverStatus.
/// </summary>
public class GetMyWarrantiesQuery : IRequest<List<MyWarrantyDto>>
{
    public Guid ReservationId { get; set; }
}

public class MyWarrantyDto
{
    public Guid Id { get; set; }
    public Guid UnitId { get; set; }
    public string WarrantyTypeCode { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public bool IsActive { get; set; }
}
