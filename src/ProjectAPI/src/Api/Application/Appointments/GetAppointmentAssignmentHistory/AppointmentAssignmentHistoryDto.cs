namespace ProjectAPI.Api.Application.Appointments.GetAppointmentAssignmentHistory;

public class AppointmentAssignmentHistoryDto
{
    public Guid Id { get; set; }
    public string? SalesAgentId { get; set; }
    public string? SalesAgentFullName { get; set; }
    public string? PreviousSalesAgentId { get; set; }
    public string? PreviousSalesAgentFullName { get; set; }
    public string AssignmentSource { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? ActorUserId { get; set; }
    public DateTime AssignedAt { get; set; }
}
