namespace ProjectAPI.Api.Application.Appointments.CreateAppointment;

/// <summary>
/// Represents the command to create a new appointment.
/// </summary>
public class CreateAppointmentCommand : IRequest<CreateAppointmentResponse>
{
    /// <summary>
    /// Gets or sets the unique identifier of the project for the appointment.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the agent for the appointment.
    /// </summary>
    public Guid? AgentId { get; set; }

    /// <summary>
    /// Gets or sets the date and time of the appointment.
    /// </summary>
    public DateTime AppointmentDate { get; set; }

public string? PropertyType { get; set; }
    /// <summary>
    /// Gets or sets the unique identifier of the user if authenticated.
    /// </summary>
    public string? UserId { get; set; } // Filled if authenticated

    /// <summary>
    /// Gets or sets the name of the guest if the user is not authenticated.
    /// </summary>
    public string Name { get; set; } // Guest name if not authenticated

    /// <summary>
    /// Gets or sets the last name of the guest if the user is not authenticated.
    /// </summary>
    public string LastName { get; set; } // Guest last name if not authenticated

    /// <summary>
    /// Gets or sets the email of the guest if the user is not authenticated.
    /// </summary>
    public string Email { get; set; } // Guest email if not authenticated

    /// <summary>
    /// Gets or sets the phone number of the guest if the user is not authenticated.
    /// </summary>
    public string PhoneNumber { get; set; } // Guest phone number if not authenticated
    public List<int> TypeBienIds { get; set; } = new List<int>();

    /// <summary>The visitor's message, optional.</summary>
    public string? Notes { get; set; }

}