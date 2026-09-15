namespace ProjectAPI.Domain.Projects.Entities;

/// <summary>
/// Represents a feature associated with a project.
/// </summary>
public class ProjectFeature
{
    /// <summary>
    /// Gets or sets the unique identifier of the project feature.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the feature (e.g., Swimming Pool, Mosque, Playground).
    /// </summary>
    public string Name { get; set; }
    public string Icon { get; set; }

    /// <summary>
    /// One-line selling point shown under the name — "Nos atouts" (§7.2 public
    /// project page): icon, name, short description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>Display order; lower first.</summary>
    public int SequenceNo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the identifier of the related project.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Navigation property to the related project.
    /// </summary>
    public Project Project { get; set; }
}
