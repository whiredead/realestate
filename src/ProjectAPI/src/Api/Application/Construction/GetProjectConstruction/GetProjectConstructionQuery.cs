namespace ProjectAPI.Api.Application.Construction.GetProjectConstruction;

/// <summary>Milestones and published updates for a project (§15).</summary>
public class GetProjectConstructionQuery : IRequest<GetProjectConstructionResponse>
{
    public Guid ProjectId { get; set; }
}

public class GetProjectConstructionResponse
{
    public Guid ProjectId { get; set; }
    public List<MilestoneDto> Milestones { get; set; } = new();
    public List<UpdateDto> Updates { get; set; } = new();
    public decimal ComputedProgressPercent { get; set; }
    public DateTime CalculatedAt { get; set; }
}

public class MilestoneDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameFr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public int SequenceNo { get; set; }
    public decimal WeightPercent { get; set; }
    public DateTime? PlannedDate { get; set; }
    public DateTime? ActualDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsValidated { get; set; }
    public bool VisibleToBuyer { get; set; }
    public bool VisibleToPublic { get; set; }
}

public class UpdateDto
{
    public Guid Id { get; set; }
    public int VersionNo { get; set; }
    public decimal ProgressPercent { get; set; }
    public string TitleFr { get; set; } = string.Empty;
    public string? DescriptionFr { get; set; }
    public string? MediaUrls { get; set; }
    public string Visibility { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
