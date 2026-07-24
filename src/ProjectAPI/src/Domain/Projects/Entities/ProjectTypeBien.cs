namespace ProjectAPI.Domain.Projects.Entities;

/// <summary>
/// Represents the many-to-many relationship between Projects and TypeBiens.
/// </summary>
public class ProjectTypeBien
{
    /// <summary>
    /// Gets or sets the unique identifier of the Project-TypeBien association.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the foreign key to the Project.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the foreign key to the TypeBien.
    /// </summary>
    public int? TypeBienId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the Project.
    /// </summary>
    public Project? Project { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the TypeBien.
    /// </summary>
    public ProjectAPI.Domain.Immeubles.Entities.TypeBien? TypeBien { get; set; }
}