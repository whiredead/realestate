namespace ProjectAPI.Api.Application.Reservations.CreateReservation;

public class CreateReservationCommand : IRequest<CreateReservationResponse>
{
    public string? BuyerId { get; set; }
    public string? Name { get; set; }
    public string? LastName { get; set; }
    public string? CIN { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public Guid UnitId { get; set; }
    public string? AgentId { get; set; }
    public string? NotaireId { get; set; }
    public decimal TotalPropertyPrice { get; set; }
    public decimal ReservationAmount { get; set; }
    public bool IsUnderConstruction { get; set; }
}
