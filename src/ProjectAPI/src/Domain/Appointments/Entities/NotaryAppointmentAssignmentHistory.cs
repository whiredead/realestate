namespace ProjectAPI.Domain.Appointments.Entities;

/// <summary>
/// Append-only log of every notary assignment/reassignment on a notary
/// appointment. Mirrors AppointmentAssignmentHistory (commercial side).
/// NotaryAppointment's own NotaireId/PreviousNotaireId/ReassignmentReason
/// hold only the CURRENT assignment — a single PreviousNotaireId cannot
/// retain more than one prior reassignment, so every assignment event,
/// including the very first one, also gets a row here, in order, forever.
/// </summary>
public class NotaryAppointmentAssignmentHistory
{
    public Guid Id { get; set; }

    public Guid NotaryAppointmentId { get; set; }
    public NotaryAppointment NotaryAppointment { get; set; } = null!;

    /// <summary>Notary this appointment moved to.</summary>
    public string? NotaireId { get; set; }

    /// <summary>Notary this appointment moved away from. Null for the first assignment.</summary>
    public string? PreviousNotaireId { get; set; }

    /// <summary>
    /// "MANUAL" (initial pick by buyer/agent/admin at booking) or
    /// "MANUAL_REASSIGNMENT" (a later change) — notaries have no
    /// auto-assignment strategy, unlike sales agents.
    /// </summary>
    public string AssignmentSource { get; set; } = string.Empty;

    /// <summary>Free text — required for MANUAL_REASSIGNMENT, optional otherwise.</summary>
    public string? Reason { get; set; }

    public string? ActorUserId { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
