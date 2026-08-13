namespace ProjectAPI.Domain.Users.Entities;

/// <summary>Ad-hoc blocked time range for a sales agent — leave, closure, or any one-off unavailability. Mirrors NotaryBlock.</summary>
public class AgentBlock
{
    public Guid Id { get; set; }
    public string AgentId { get; set; }
    public DateTime Start { get; set; }    // exact UTC
    public DateTime End { get; set; }
    public string Reason { get; set; }    // optional

    public Agent Agent { get; set; } = null!;
}
