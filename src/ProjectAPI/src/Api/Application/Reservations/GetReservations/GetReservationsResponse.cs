using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Domain.Reservations.Entities;

namespace ProjectAPI.Api.Application.Reservations.GetReservations;
public class GetReservationsResponse
{
    public Guid Id { get; set; }

    // Buyer Information
    public string? BuyerId { get; set; }
    public string? Name { get; set; }
    public string? LastName { get; set; }
    public string? CIN { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }

    // Reservation Details
    public Guid UnitId { get; set; }
    public string? UnitDetails { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string? AgentId { get; set; }
    public string? NotaireId { get; set; }
    public decimal TotalPropertyPrice { get; set; }
    public decimal ReservationAmount { get; set; }
    public DateTime ReservationDate { get; set; }
    public bool IsUnderConstruction { get; set; }

    // New: status & admin “stuff”
    public ReservationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public string? ValidatedBy { get; set; }
    public string? AdminNote { get; set; }

    // New: documents
    public List<ReservationDocumentResponse> Documents { get; set; } = new();
}
