namespace ProjectAPI.Domain.FeedBacks.Entities;
/// <summary>
/// Represents an incident reported by a user for after-sales services.
/// </summary>
public class Incident
{
    public Guid Id { get; set; }
    public string UserId { get; set; } // The user reporting the incident
    public Guid UnitId { get; set; } // The unit affected
    public string Description { get; set; } // Description of the issue
    public string Status { get; set; } // e.g., Reported, In Progress, Resolved
    public DateTime ReportedAt { get; set; }
}
