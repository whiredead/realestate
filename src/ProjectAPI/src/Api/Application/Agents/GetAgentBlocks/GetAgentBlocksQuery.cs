namespace ProjectAPI.Api.Application.Agents.GetAgentBlocks;

public class GetAgentBlocksQuery : IRequest<List<AgentBlockDto>>
{
    public string AgentId { get; set; } = null!;
    public DateTime? From { get; set; } // UTC
    public DateTime? To { get; set; } // UTC
}
