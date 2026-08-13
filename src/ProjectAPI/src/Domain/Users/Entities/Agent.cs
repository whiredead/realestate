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

    /// <summary>Ad-hoc blocked time ranges — leave, closures, one-off unavailability.</summary>
    public ICollection<AgentBlock> Blocks { get; set; } = new List<AgentBlock>();

    /// <summary>Recurring weekly working-hours template.</summary>
    public ICollection<AgentWeeklyAvailability> WeeklyAvailabilities { get; set; } = new List<AgentWeeklyAvailability>();

    /// <summary>Date-specific overrides of the weekly schedule.</summary>
    public ICollection<AgentDateOverride> DateOverrides { get; set; } = new List<AgentDateOverride>();

    /// <summary>Optional per-agent slot duration/buffer configuration.</summary>
    public AgentAppointmentSettings? AppointmentSettings { get; set; }
}