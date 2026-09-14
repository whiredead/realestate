using ProjectAPI.Api.Application.Common.Units;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Sales.Entities;

namespace ProjectAPI.Api.Application.Sales.SaleDrafts;

/// <summary>
/// A sale as the console shows it (§5.7, §6).
///
/// <see cref="IsEditable"/> is computed here rather than left to the client:
/// the state machine that decides it is server-side, and a frontend that
/// re-derived "can I still edit this" from the status string would be a second,
/// drifting copy of the same rule.
/// </summary>
public class SaleResponse : IHasUnitLocation
{
    public Guid Id { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid UnitId { get; set; }

    // Where the sale sits (§ vente : projet, immeuble, étage, unité).
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public Guid? ImmeubleId { get; set; }
    public string? ImmeubleName { get; set; }
    public string? FloorName { get; set; }
    public string? UnitNumber { get; set; }
    public UnitContextDto? UnitContext { get; set; }

    public string Status { get; set; } = string.Empty;
    public bool IsEditable { get; set; }

    public string BuyerFirstName { get; set; } = string.Empty;
    public string BuyerLastName { get; set; } = string.Empty;
    public string BuyerEmail { get; set; } = string.Empty;
    public string BuyerPhoneNumber { get; set; } = string.Empty;
    public string? BuyerCIN { get; set; }

    public DateTime SaleDate { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal? FinalPrice { get; set; }
    public decimal? ReservationAmount { get; set; }
    public decimal? RemainingAmount { get; set; }

    public int WarrantyMonths { get; set; }
    public string? Notes { get; set; }

    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    /// <summary>
    /// Builds the response with the sale's location resolved (unit → floor →
    /// building → project). Every endpoint returning a sale uses this, so the
    /// screen never has to re-derive where the sale is.
    /// </summary>
    public static async Task<SaleResponse> FromAsync(ProjectAPI.Infrastructure.Context.ApplicationDbContext db, Sale sale, CancellationToken ct)
    {
        var response = From(sale);
        var contexts = await UnitLocations.ForUnitsAsync(db, new[] { sale.UnitId }, ct);
        return response.WithLocation(contexts.GetValueOrDefault(sale.UnitId));
    }

    public static SaleResponse From(Sale sale) => new()
    {
        Id = sale.Id,
        ReservationId = sale.ReservationId,
        UnitId = sale.UnitId,
        Status = sale.Status.ToString(),
        IsEditable = SaleStateMachine.IsEditable(sale.Status),
        BuyerFirstName = sale.BuyerFirstName,
        BuyerLastName = sale.BuyerLastName,
        BuyerEmail = sale.BuyerEmail,
        BuyerPhoneNumber = sale.BuyerPhoneNumber,
        BuyerCIN = sale.BuyerCIN,
        SaleDate = sale.SaleDate,
        TotalPrice = sale.TotalPrice,
        FinalPrice = sale.FinalPrice,
        ReservationAmount = sale.ReservationAmount,
        RemainingAmount = sale.RemainingAmount,
        WarrantyMonths = sale.WarrantyMonths,
        Notes = sale.Notes,
        CreatedBy = sale.CreatedBy,
        CreatedAt = sale.CreatedAt,
        ConfirmedAt = sale.ConfirmedAt
    };
}
