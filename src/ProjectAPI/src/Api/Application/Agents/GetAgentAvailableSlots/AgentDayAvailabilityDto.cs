namespace ProjectAPI.Api.Application.Agents.GetAgentAvailableSlots;

public class AgentDayAvailabilityDto
{
    public DateOnly Date { get; set; }
    public List<TimeSlotDto> FreeSlots { get; set; } = new();
}
