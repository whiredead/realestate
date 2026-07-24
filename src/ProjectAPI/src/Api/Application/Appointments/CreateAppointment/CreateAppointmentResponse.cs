namespace ProjectAPI.Api.Application.Appointments.CreateAppointment;

/// <summary>
/// Represents the response returned after creating an appointment.
/// </summary>
public class CreateAppointmentResponse
{
    /// <summary>
    /// Gets or sets the unique identifier of the created appointment.
    /// </summary>
    public Guid AppointmentId { get; set; }

    /// <summary>
    /// Gets or sets the message indicating the outcome of the appointment creation.
    /// </summary>
    public string Message { get; set; }
}