namespace ProjectAPI.Api.Application.Floors.GetFloorsByImmeuble;

/// <summary>Lists the floors of a building, in display order — needed to populate a unit-creation form's floor picker.</summary>
public class GetFloorsByImmeubleQuery : IRequest<List<FloorSummary>>
{
    public Guid ImmeubleId { get; set; }
}

public class FloorSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SequenceNo { get; set; }
}
