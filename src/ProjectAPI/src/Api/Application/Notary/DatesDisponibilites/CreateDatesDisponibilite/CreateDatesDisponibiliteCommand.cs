namespace ProjectAPI.Api.Application.Notary.DatesDisponibilites.CreateDatesDisponibilite;
public class CreateDatesDisponibiliteCommand : IRequest<CreateDatesDisponibiliteResponse>
{
    public string? NotaireId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "Available"; // "Available" or "Unavailable"
    public DateTime DateCreation { get; set; } = DateTime.Now;
}
