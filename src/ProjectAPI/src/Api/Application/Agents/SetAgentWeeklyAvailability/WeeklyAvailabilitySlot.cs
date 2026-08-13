namespace ProjectAPI.Api.Application.Agents.SetAgentWeeklyAvailability;

public class WeeklyAvailabilitySlot
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}
