namespace ProjectAPI.Api.Application.Units.GetUnitsByFloor;

/// <summary>
/// Drill-down: "Étage → Unité" — every unit on one floor, enriched with
/// buyer/agent info when sold (joined from Reservation — the Sales module is
/// frozen per work order #2, so this reads Reservation.Name/LastName/AgentId
/// rather than extending Sale's DTOs).
/// </summary>
public class GetUnitsByFloorQuery : IRequest<List<UnitDrillDownDto>>
{
    public Guid FloorId { get; set; }
}

public class UnitDrillDownDto
{
    public Guid Id { get; set; }
    public Guid FloorId { get; set; }
    public string UnitNumber { get; set; } = string.Empty;
    public int? NumberOfBedrooms { get; set; }
    public int? NumberOfBathrooms { get; set; }
    public double? ApartmentSurface { get; set; }
    public double? BalconySurface { get; set; }
    public double? TerraceSurface { get; set; }
    public double? GardenSurface { get; set; }
    public double? TotalSurface { get; set; }
    public double? SaleableValue { get; set; }
    public double? SaleableValue1 { get; set; }
    public string? View { get; set; }
    public string? Orientation { get; set; }
    public decimal? LatestPrice { get; set; }
    public decimal? PriceSaleableValue { get; set; }
    public decimal? PriceSaleableValue1 { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Images { get; set; }

    /// <summary>Populated only when the unit is RESERVED/CONTRACTED/SOLD/DELIVERED — the reservation that put it in that state.</summary>
    public string? BuyerName { get; set; }
    public string? AgentId { get; set; }
    public string? AgentName { get; set; }
}
