namespace ProjectAPI.Api.Application.Common.Models;

/// <summary>
/// Enum representing the possible statuses of an appointment.
/// </summary>
public enum AppointmentStatus
{
    EnCoursDeTraitement,
    Confirmed,
    ConfirmedByUser,
    Cancelled,
    Completed
}

/// <summary>
/// Model representing the response for an appointment.
/// </summary>
public class AppointmentResponse
{
    /// <summary>
    /// Gets or sets the unique identifier of the appointment.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the user associated with the appointment.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the project related to the appointment.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the date of the appointment.
    /// </summary>
    public DateTime Date { get; set; }
    public string? PropertyType { get; set; }

    /// <summary>
    /// Gets or sets the status of the appointment.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets any additional notes related to the appointment.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the agent associated with the appointment.
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// Gets or sets the full name of the agent associated with the appointment.
    /// </summary>
    public string AgentFullName { get; set; }

    /// <summary>
    /// Gets or sets the name of the project related to the appointment.
    /// </summary>
    public string ProjectName { get; set; }


    /// <summary>
    /// Gets or sets the date and time of the appointment.
    /// </summary>
    public DateTime AppointmentDate { get; set; }

    /// <summary>
    /// Gets or sets the first name of the user involved in the appointment.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the last name of the user involved in the appointment.
    /// </summary>
    public string LastName { get; set; }

    /// <summary>
    /// Gets or sets the email address of the user involved in the appointment.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the phone number of the user involved in the appointment.
    /// </summary>
    public string PhoneNumber { get; set; }
    public List<int> TypeBienIds { get; set; } = new List<int>();

    /// <summary>How AgentId was chosen: "EXISTING_OWNER" | "ROUND_ROBIN" | "LOWEST_WORKLOAD" | "PRIMARY_AGENT" | "MANUAL_REASSIGNMENT". Null if never assigned.</summary>
    public string? AssignmentSource { get; set; }

    public DateTime? AssignedAt { get; set; }

    /// <summary>The agent this appointment was assigned to immediately before the current one, if any.</summary>
    public string? PreviousSalesAgentId { get; set; }

    public string? ReassignmentReason { get; set; }

    /// <summary>When this row exists because a confirmed appointment was reassigned/rescheduled, this points at the row it replaced.</summary>
    public Guid? PreviousAppointmentId { get; set; }
    public DateTime? CreatedAt { get; set; }
}