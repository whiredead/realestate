namespace ProjectAPI.Api.Application.Agents.DeleteAgentBlock;

public class DeleteAgentBlockCommand : IRequest<bool>
{
    public string AgentId { get; set; } = null!;
    public Guid BlockId { get; set; }
}
