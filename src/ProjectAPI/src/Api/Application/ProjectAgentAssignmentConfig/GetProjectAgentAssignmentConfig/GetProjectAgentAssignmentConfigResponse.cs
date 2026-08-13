namespace ProjectAPI.Api.Application.ProjectAgentAssignmentConfig.GetProjectAgentAssignmentConfig;

public class GetProjectAgentAssignmentConfigResponse
{
    public Guid ProjectId { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public string? PrimaryAgentId { get; set; }
    public string? LastAssignedAgentId { get; set; }
    public DateTime? LastAssignedAt { get; set; }
}
