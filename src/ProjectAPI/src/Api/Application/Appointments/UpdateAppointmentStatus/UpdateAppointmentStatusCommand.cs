namespace ProjectAPI.Api.Application.Appointments.UpdateAppointmentStatus;

/// <summary>
/// Command to transition a commercial appointment through the shared §47.3
/// machine: confirm, propose a new slot, reject, cancel, complete or mark
/// no-show.
/// </summary>
public class UpdateAppointmentStatusCommand : IRequest<UpdateAppointmentStatusResponse>
{
    /// <summary>
    /// Gets or sets the unique identifier of the appointment.
    /// </summary>
    public Guid AppointmentId { get; set; }

    /// <summary>
    /// Gets or sets the target status (REQUESTED, CONFIRMED, RESCHEDULE_PROPOSED,
    /// REJECTED, CANCELLED, COMPLETED, NO_SHOW — any AppointmentAttemptStatus name).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>New slot, required when proposing a reschedule.</summary>
    public DateTime? ProposedDate { get; set; }

    /// <summary>Motive for rejection or cancellation (§10.1).</summary>
    public string? Reason { get; set; }

    public string? ActorUserId { get; set; }

    /// <summary>
    /// Reassigns this appointment to a different eligible agent. Independent
    /// of Status/ProposedDate — a reassignment can accompany a status
    /// transition or stand alone. Required together with
    /// <see cref="ReassignmentReason"/>.
    /// </summary>
    public string? NewSalesAgentId { get; set; }

    /// <summary>Required when NewSalesAgentId is supplied.</summary>
    public string? ReassignmentReason { get; set; }
}
