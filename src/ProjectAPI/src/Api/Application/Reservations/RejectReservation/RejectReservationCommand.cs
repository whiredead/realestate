namespace ProjectAPI.Api.Application.Reservations.RejectReservation;

public class RejectReservationCommand : IRequest<bool>
{
    public Guid ReservationId { get; set; }
    public string AdminUserId { get; set; } = null!;
    public string? Reason { get; set; }
}
