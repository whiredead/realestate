namespace ProjectAPI.Api.Application.Floors.CreateFloor;

/// <summary>Creates a floor under a building — the missing middle level of Project → Building → Floor → Unit.</summary>
public class CreateFloorCommand : IRequest<CreateFloorResponse>
{
    public Guid ImmeubleId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Ordering within the building. Defaults to "after the last floor" when omitted.</summary>
    public int? SequenceNo { get; set; }
}

public class CreateFloorResponse
{
    public Guid FloorId { get; set; }
    public string Message { get; set; } = string.Empty;
}
