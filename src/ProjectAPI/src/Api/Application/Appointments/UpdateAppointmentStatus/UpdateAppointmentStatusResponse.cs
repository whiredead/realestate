namespace ProjectAPI.Api.Application.Appointments.UpdateAppointmentStatus;

/// <summary>
/// Response for updating the status of an appointment.
/// </summary>
public class UpdateAppointmentStatusResponse
{
    /// <summary>
    /// Gets or sets the unique identifier of the updated appointment.
    /// </summary>
    public Guid AppointmentId { get; set; }

    /// <summary>
    /// Gets or sets the new status of the updated appointment.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the message confirming the update.
    /// </summary>
    public string Message { get; set; }
}
