namespace ProjectAPI.Domain.Appointments.Entities;

/// <summary>
/// Append-only log of every agent assignment/reassignment on a commercial
/// appointment. Appointment's own SalesAgentId/AssignmentSource/AssignedAt/
/// AssignedByUserId/PreviousSalesAgentId/ReassignmentReason hold only the
/// CURRENT assignment — a single PreviousSalesAgentId field cannot retain
/// more than one prior reassignment, so every assignment event (including
/// the very first one) also gets a row here, in order, forever.
/// </summary>
public class AppointmentAssignmentHistory
{
    public Guid Id { get; set; }

    public Guid AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;

    /// <summary>Agent this appointment moved to. Null only if an assignment attempt failed to find anyone (logged for diagnosis, not expected in normal operation).</summary>
    public string? SalesAgentId { get; set; }

    /// <summary>Agent this appointment moved away from. Null for the first assignment.</summary>
    public string? PreviousSalesAgentId { get; set; }

    /// <summary>"EXISTING_OWNER" | "ROUND_ROBIN" | "LOWEST_WORKLOAD" | "PRIMARY_AGENT" | "MANUAL_REASSIGNMENT".</summary>
    public string AssignmentSource { get; set; } = string.Empty;

    /// <summary>Free text — required for MANUAL_REASSIGNMENT, optional otherwise.</summary>
    public string? Reason { get; set; }

    /// <summary>Who caused this event: the acting admin/agent, or null for a system-driven automatic assignment.</summary>
    public string? ActorUserId { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
