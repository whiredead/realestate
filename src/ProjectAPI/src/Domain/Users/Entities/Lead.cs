using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Domain.Users.Entities;


/// <summary>
/// Represents a lead generated for a project by a user.
/// </summary>
public class Lead
{
    public Guid Id { get; set; }
    public string? UserId { get; set; } // Nullable for external buyers
    public Guid ProjectId { get; set; }
    public string? AgentId { get; set; } // Nullable for cases without an assigned agent
    public DateTime CreatedAt { get; set; }
    public string? Description { get; set; } // Optional description of the lead

    // Navigation properties
    public Project Project { get; set; }
}