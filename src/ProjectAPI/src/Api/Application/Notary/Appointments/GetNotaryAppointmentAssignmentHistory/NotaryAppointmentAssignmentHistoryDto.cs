namespace ProjectAPI.Api.Application.Notary.Appointments.GetNotaryAppointmentAssignmentHistory;

public class NotaryAppointmentAssignmentHistoryDto
{
    public Guid Id { get; set; }
    public string? NotaireId { get; set; }
    public string? NotaireFullName { get; set; }
    public string? PreviousNotaireId { get; set; }
    public string? PreviousNotaireFullName { get; set; }
    public string AssignmentSource { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? ActorUserId { get; set; }
    public DateTime AssignedAt { get; set; }
}
