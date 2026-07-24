using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Domain.Reservations.Entities;

namespace ProjectAPI.Api.Application.Reservations.GetReservationById;

public class GetReservationByIdResponse
{
    public Guid Id { get; set; }
    public string? BuyerId { get; set; }
    public string? Name { get; set; }
    public string? LastName { get; set; }
    public string? CIN { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public Guid UnitId { get; set; }
    public string? UnitDetails { get; set; }
    public string? AgentId { get; set; }
    public decimal TotalPropertyPrice { get; set; }
    public decimal ReservationAmount { get; set; }
    public DateTime ReservationDate { get; set; }
    public bool IsUnderConstruction { get; set; }

    // NEW
    public ReservationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public string? ValidatedBy { get; set; }
    public string? AdminNote { get; set; }
    public List<ReservationDocumentResponse> Documents { get; set; } = new();
}
