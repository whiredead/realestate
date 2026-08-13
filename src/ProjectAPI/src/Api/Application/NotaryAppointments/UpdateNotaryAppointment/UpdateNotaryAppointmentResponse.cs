namespace ProjectAPI.Api.Application.NotaryAppointments.UpdateNotaryAppointment;

public class UpdateNotaryAppointmentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }

    /// <summary>
    /// The appointment this response describes. On a reassignment of a
    /// CONFIRMED appointment this is a NEW id — the original row is frozen
    /// as Superseded and this new one is Requested, pending the buyer's
    /// acceptance. Callers must re-fetch by THIS id, not the one they sent.
    /// </summary>
    public Guid AppointmentId { get; set; }
    public string? Status { get; set; }
}