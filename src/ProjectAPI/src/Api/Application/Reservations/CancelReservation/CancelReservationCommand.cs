namespace ProjectAPI.Api.Application.Reservations.CancelReservation;

public class CancelReservationCommand : IRequest<CancelReservationResponse>
{
    public Guid ReservationId { get; set; }
}