namespace ProjectAPI.Api.Application.Reservations.RejectReservation;

/// <summary>§5.3 "Reject ⇒ reason mandatory" — see RejectReservationValidator, which was previously absent, so this field was never actually enforced despite the handler's own comment saying otherwise.</summary>
public class RejectReservationCommand : IRequest<bool>
{
    public Guid ReservationId { get; set; }
    public string AdminUserId { get; set; } = null!;
    public required string Reason { get; set; }
}
