using ProjectAPI.Domain.Users.Entities;


namespace ProjectAPI.Domain.Projects.Entities;

public class ProjectAssignment
{
    /// <summary>
    /// Gets or sets the unique identifier for the project assignment.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the related project.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the agent assigned to the project.
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the notaire assigned to the project.
    /// </summary>
    public string? NotaryId { get; set; }
    /// <summary>
    /// Gets or sets the date and time when the assignment was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the project assignment is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the project associated with the assignment.
    /// </summary>
    public Project Project { get; set; }

    /// <summary>
    /// Gets or sets the agent assigned to the project.
    /// </summary>
    public Agent? Agent { get; set; }
    public Notary? Notary { get; set; }

}
