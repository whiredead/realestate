namespace ProjectAPI.Api.Application.Appointments.GetMyAppointments;

/// <summary>
/// §6.3/§8 — the buyer's own commercial appointments, never anyone else's.
/// Always resolves to ICurrentUser.UserId; unlike GetAppointmentsQuery (an
/// internal-staff tool), it takes no caller-supplied filter to bypass.
/// </summary>
public class GetMyAppointmentsQuery : IRequest<List<MyAppointmentSummary>>
{
}

public class MyAppointmentSummary
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string? SalesAgentId { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string? PropertyType { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
