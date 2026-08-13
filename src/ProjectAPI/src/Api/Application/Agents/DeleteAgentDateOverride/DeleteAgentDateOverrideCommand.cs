namespace ProjectAPI.Api.Application.Agents.DeleteAgentDateOverride;

public class DeleteAgentDateOverrideCommand : IRequest<bool>
{
    public string AgentId { get; set; } = null!;
    public Guid OverrideId { get; set; }
}
