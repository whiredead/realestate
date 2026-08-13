using ProjectAPI.Api.Application.Agents.SetAgentWeeklyAvailability;

namespace ProjectAPI.Api.Application.Agents.GetAgentWeeklyAvailability;

public class GetAgentWeeklyAvailabilityQuery : IRequest<List<WeeklyAvailabilitySlot>>
{
    public string AgentId { get; set; } = null!;
}
