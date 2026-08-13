namespace ProjectAPI.Domain.Users.Entities;

/// <summary>
/// Date-specific override of a sales agent's normal weekly schedule — used
/// for leave, closures, or extra one-off availability on a given date range.
/// Mirrors NotaryDateDisponibilite. Distinct from AgentBlock: a block is a
/// narrow ad-hoc busy window carved out of an otherwise-available day; an
/// override replaces the day's status wholesale (e.g. "closed all of
/// Aug 15-16" or "available Sat Aug 20 despite weekly schedule saying no").
/// </summary>
public class AgentDateOverride
{
    public Guid Id { get; set; }
    public string AgentId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "Available";  // "Available" or "Unavailable"
    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
    public Agent Agent { get; set; } = null!;
}
