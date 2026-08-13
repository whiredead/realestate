namespace ProjectAPI.Api.Application.Agents.CreateAgentBlock;

public class CreateAgentBlockCommand : IRequest<CreateAgentBlockResponse>
{
    // Nullable: supplied by the controller from the route, not the body.
    public string? AgentId { get; set; }
    public DateTime Start { get; init; }
    public DateTime End { get; init; }
    public string Reason { get; init; } = "";
}
