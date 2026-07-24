namespace ProjectAPI.Domain.Users.Entities;

public class NotaryDateDisponibilite
{
    public Guid Id { get; set; }
    public string NotaryId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "Available";  // "Available" or "Unavailable"
    public DateTime DateCreation { get; set; } = DateTime.Now;
    public Notary Notary { get; set; } = null!;
}