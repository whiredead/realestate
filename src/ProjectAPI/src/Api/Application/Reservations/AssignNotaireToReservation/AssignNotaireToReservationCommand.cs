namespace ProjectAPI.Api.Application.Reservations.AssignNotaireToReservation;

public class AssignNotaireToReservationCommand : IRequest<AssignNotaireToReservationResponse>
{
    public Guid ReservationId { get; set; }
    public string? NotaireId { get; set; }
}