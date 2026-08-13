namespace ProjectAPI.Api.Application.ProjectAgentAssignmentConfig.SetProjectAgentAssignmentConfig;

/// <summary>Creates or updates a project's sales-agent assignment strategy.</summary>
public class SetProjectAgentAssignmentConfigCommand : IRequest<SetProjectAgentAssignmentConfigResponse>
{
    public Guid ProjectId { get; set; }

    /// <summary>"ROUND_ROBIN" | "LOWEST_WORKLOAD" | "PRIMARY_AGENT".</summary>
    public string RuleType { get; set; } = string.Empty;

    /// <summary>Required when RuleType = PRIMARY_AGENT; ignored otherwise.</summary>
    public string? PrimaryAgentId { get; set; }

    public string? UpdatedByUserId { get; set; }
}
