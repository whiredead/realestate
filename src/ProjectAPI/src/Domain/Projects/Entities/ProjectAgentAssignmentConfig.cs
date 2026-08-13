namespace ProjectAPI.Domain.Projects.Entities;

/// <summary>
/// One record per project — how a sales agent is auto-picked for a new
/// commercial appointment when the prospect has no existing responsible
/// agent (see SalesAgentAssignmentService). At most one row per ProjectId
/// (enforced by a unique index — see ProjectAgentAssignmentConfigConfiguration).
/// </summary>
public class ProjectAgentAssignmentConfig
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// <summary>"ROUND_ROBIN" | "LOWEST_WORKLOAD" | "PRIMARY_AGENT".</summary>
    public string RuleType { get; set; } = string.Empty;

    /// <summary>Only meaningful when RuleType = PRIMARY_AGENT. Must hold an active SALES_AGENT ProjectMembership for this project (validated at write time).</summary>
    public string? PrimaryAgentId { get; set; }

    /// <summary>Round-robin rotation pointer — the last agent picked by this rule for this project.</summary>
    public string? LastAssignedAgentId { get; set; }
    public DateTime? LastAssignedAt { get; set; }

    public string? UpdatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
