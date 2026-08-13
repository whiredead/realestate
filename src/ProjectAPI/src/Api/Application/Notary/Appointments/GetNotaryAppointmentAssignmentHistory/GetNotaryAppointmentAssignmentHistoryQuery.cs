namespace ProjectAPI.Api.Application.Notary.Appointments.GetNotaryAppointmentAssignmentHistory;

/// <summary>
/// Full assignment/reassignment history for a notary appointment, walking
/// back through PreviousAppointmentId when a post-confirmation reassignment
/// created a new linked row. Mirrors GetAppointmentAssignmentHistoryQuery
/// (commercial side).
/// </summary>
public class GetNotaryAppointmentAssignmentHistoryQuery : IRequest<List<NotaryAppointmentAssignmentHistoryDto>>
{
    public Guid NotaryAppointmentId { get; set; }

    /// <summary>When set (a NOTARY caller), the caller must appear as either NotaireId or PreviousNotaireId on at least one row in the chain.</summary>
    public string? RestrictToNotaryId { get; set; }
}
