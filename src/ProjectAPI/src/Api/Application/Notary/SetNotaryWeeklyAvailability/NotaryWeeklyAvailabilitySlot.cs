namespace ProjectAPI.Api.Application.Notary.SetNotaryWeeklyAvailability;

public class NotaryWeeklyAvailabilitySlot
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}
