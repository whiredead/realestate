namespace ProjectAPI.Api.Application.Reservations.SoldReservation;

public class SoldReservationCommand : IRequest<SoldReservationResponse>
{
    public Guid ReservationId { get; set; }
}