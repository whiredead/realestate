namespace ProjectAPI.Api.Application.Agents.GetAgentAvailableSlots;

public class GetAgentAvailableSlotsQuery : IRequest<List<AgentDayAvailabilityDto>>
{
    public string AgentId { get; set; } = null!;
    public DateTime From { get; set; } // UTC
    public DateTime To { get; set; } // UTC
}
