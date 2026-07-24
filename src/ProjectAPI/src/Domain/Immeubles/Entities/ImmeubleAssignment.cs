using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Domain.Immeubles.Entities;

/// <summary>
/// Represents an assignment of an agent to a project, including assignment details such as project, agent, and assignment status.
/// </summary>
public class ImmeubleAssignment
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
    public string AgentId { get; set; }

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
    public Immeuble Immeuble { get; set; }

    /// <summary>
    /// Gets or sets the agent assigned to the project.
    /// </summary>
    public Agent Agent { get; set; }
}