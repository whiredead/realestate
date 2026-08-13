namespace ProjectAPI.Api.Application.Agents.GetAgentBlocks;

public class AgentBlockDto
{
    public Guid Id { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string? Reason { get; set; }
}
