namespace ProjectAPI.Api.Application.Appointments.UpdateAppointmentStatus;

/// <summary>
/// Command to update the status of an appointment.
/// </summary>
public class UpdateAppointmentStatusCommand : IRequest<UpdateAppointmentStatusResponse>
{
    /// <summary>
    /// Gets or sets the unique identifier of the appointment.
    /// </summary>
    public Guid AppointmentId { get; set; }

    /// <summary>
    /// Gets or sets the new status of the appointment (e.g., Approved, Cancelled, Completed).
    /// </summary>
    public string Status { get; set; }
}
