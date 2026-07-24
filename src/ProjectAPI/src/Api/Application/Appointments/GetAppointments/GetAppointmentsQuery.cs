using ProjectAPI.Api.Application.Common.Models;

namespace ProjectAPI.Api.Application.Appointments.GetAppointments;

/// <summary>
/// Query to get a list of appointments with optional filters.
/// </summary>
public class GetAppointmentsQuery : IRequest<PaginatedResponse<AppointmentResponse>>
{
    /// <summary>
    /// Gets or sets the project ID to filter appointments by project.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the agent ID to filter appointments by agent.
    /// </summary>
    public string? AgentId { get; set; }

    /// <summary>
    /// Gets or sets the date to filter appointments by the appointment date.
    /// </summary>
    public DateTime? AppointmentDate { get; set; }

    /// <summary>
    /// Gets or sets the user ID to filter appointments by user.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the status to filter appointments by status.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets the first name to filter appointments by user first name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the last name to filter appointments by user last name.
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Gets or sets the email to filter appointments by user email.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the phone number to filter appointments by user phone number.
    /// </summary>
    public string? PhoneNumber { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}