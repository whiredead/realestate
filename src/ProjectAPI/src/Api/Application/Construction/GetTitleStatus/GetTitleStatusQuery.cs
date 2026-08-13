namespace ProjectAPI.Api.Application.Construction.GetTitleStatus;

/// <summary>Land-title status and history for a unit (§16).</summary>
public class GetTitleStatusQuery : IRequest<GetTitleStatusResponse>
{
    public Guid UnitId { get; set; }
}

public class GetTitleStatusResponse
{
    public Guid UnitId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? StatusAt { get; set; }
    public string? DocumentUrl { get; set; }
    public bool AllowsNotaryAppointment { get; set; }
    public List<TitleHistoryEntryDto> History { get; set; } = new();
}

public class TitleHistoryEntryDto
{
    public Guid Id { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string? Reason { get; set; }
    public string? DocumentUrl { get; set; }
}
