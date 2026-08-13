namespace ProjectAPI.Api.Application.Immeubles.GetImmeubleFloorStats;

/// <summary>
/// Drill-down: "Immeuble → Étage" — every floor in a building with a live
/// unit-status summary, the level between the building's own roll-up
/// (ImmeubleResponse/GetImmeubleByIdHandler) and the individual unit list
/// (GetUnitsByProjectIdQuery, optionally narrowed to one floor).
/// </summary>
public class GetImmeubleFloorStatsQuery : IRequest<List<FloorStatsDto>>
{
    public Guid ImmeubleId { get; set; }
}

public class FloorStatsDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SequenceNo { get; set; }
    public int TotalUnits { get; set; }
    public int AvailableUnits { get; set; }
    public int ReservedUnits { get; set; }
    public int SoldUnits { get; set; }
}
