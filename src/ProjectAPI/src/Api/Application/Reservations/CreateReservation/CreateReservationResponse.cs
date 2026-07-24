namespace ProjectAPI.Api.Application.Reservations.CreateReservation;
public class CreateReservationResponse
{
    public Guid ReservationId { get; set; }
    public string Message { get; set; } = "Reservation created (Pending).";
    public bool Success { get; set; } = true;
    public string Details { get; set; }
}
