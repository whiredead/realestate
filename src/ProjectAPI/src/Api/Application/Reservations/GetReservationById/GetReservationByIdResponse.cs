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

    /// <summary>§5.3 — the catalogue price at submit time, before any discount.</summary>
    public decimal? CatalogPrice { get; set; }

    /// <summary>§5.3 — discount granted, if any.</summary>
    public decimal Discount { get; set; }

    /// <summary>§5.3 — "final_price = catalog_price - discount", frozen at submit.</summary>
    public decimal? FinalPrice { get; set; }

    public string? NotaireId { get; set; }

    /// <summary>Deadline after which a SUBMITTED/CHANGES_REQUESTED reservation expires (§12.3).</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>§5.3/§6.2 — co-buyers on this file, each with an ownership percentage. The primary buyer is not repeated here.</summary>
    public List<ReservationCoBuyerResponse> CoBuyers { get; set; } = new();
}

public class ReservationCoBuyerResponse
{
    public Guid CrmContactId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public decimal? OwnershipPercent { get; set; }
}
