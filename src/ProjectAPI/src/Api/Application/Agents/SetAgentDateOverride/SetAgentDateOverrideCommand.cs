namespace ProjectAPI.Api.Application.Agents.SetAgentDateOverride;

/// <summary>Creates a date-range override (leave, closure, or extra availability) for an agent.</summary>
public class SetAgentDateOverrideCommand : IRequest<SetAgentDateOverrideResponse>
{
    // Nullable: supplied by the controller from the route, not the body.
    public string? AgentId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    /// <summary>"Available" or "Unavailable".</summary>
    public string Status { get; set; } = "Unavailable";
}
