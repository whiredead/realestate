namespace ProjectAPI.Api.Application.ProjectAgentAssignmentConfig.SetProjectAgentAssignmentConfig;

public class SetProjectAgentAssignmentConfigResponse
{
    public Guid ProjectId { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public string? PrimaryAgentId { get; set; }
}
