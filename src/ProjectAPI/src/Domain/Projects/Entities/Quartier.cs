namespace ProjectAPI.Domain.Projects.Entities;

/// <summary>
/// Represents a district or area (quartier) in which projects are located.
/// </summary>
public class Quartier
{
    /// <summary>
    /// Gets or sets the unique identifier of the quartier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The name of the quartier (e.g., Eco City Zenata).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// A text description of the quartier.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// An optional string containing images or links to images of the quartier.
    /// This could be JSON or comma-separated URLs.
    /// </summary>
    public string Images { get; set; }

    /// <summary>
    /// Navigation property for all projects within this quartier.
    /// </summary>
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
