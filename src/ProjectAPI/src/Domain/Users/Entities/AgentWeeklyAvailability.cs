namespace ProjectAPI.Domain.Users.Entities;

/// <summary>Recurring weekly working-hours template for a sales agent. Mirrors WeeklyAvailability (Notary).</summary>
public class AgentWeeklyAvailability
{
    public Guid Id { get; set; }
    public string AgentId { get; set; }      // FK to AspNetUsers(Id) where Discriminator='Agent'
    public DayOfWeek DayOfWeek { get; set; } // Monday, Tuesday, …
    public TimeSpan StartTime { get; set; } // e.g. 09:00
    public TimeSpan EndTime { get; set; } // e.g. 17:00

    public Agent Agent { get; set; } = null!;
}
