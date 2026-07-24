using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Domain.Users.Entities;

/// <summary>
/// Represents an agent, which is a type of user, responsible for managing projects and appointments.
/// </summary>
public class Agent : User
{
    /// <summary>
    /// Gets or sets the description about the agent.
    /// </summary>
    public string About { get; set; }

    /// <summary>
    /// Gets or sets the rating of the agent. Defaults to 0.
    /// </summary>
    public double Rating { get; set; } = 0;

    /// <summary>
    /// Gets or sets the collection of project assignments associated with the agent.
    /// </summary>

    /// <summary>
    /// Gets or sets the collection of appointments associated with the agent.
    /// </summary>
    public ICollection<Appointment> Appointments { get; set; }
    public ICollection<PerformanceIndicator> PerformanceIndicators { get; set; }
    public ICollection<ProjectAssignment> Assignments { get; set; } = new List<ProjectAssignment>();



}