namespace AuthenticationAPI.Domain.ApplicationUser.Entities;

public class WeeklyAvailability
{
    public Guid Id { get; set; }
    public string NotaryId { get; set; }      // FK to AspNetUsers(Id) where Discriminator='Notary'
    public DayOfWeek DayOfWeek { get; set; } // Monday, Tuesday, …
    public TimeSpan StartTime { get; set; } // e.g. 09:00
    public TimeSpan EndTime { get; set; } // e.g. 17:00

}
