namespace ProjectAPI.Api.Application.Appointments.GetAppointmentAssignmentHistory;

/// <summary>
/// Full assignment/reassignment history for an appointment, walking back
/// through PreviousAppointmentId when a post-confirmation reassignment
/// created a new linked row (see UpdateAppointmentStatusHandler) — so the
/// caller sees the whole chain from one call, not just the current row's
/// own history entries.
/// </summary>
public class GetAppointmentAssignmentHistoryQuery : IRequest<List<AppointmentAssignmentHistoryDto>>
{
    public Guid AppointmentId { get; set; }

    /// <summary>
    /// When set (a SALES_AGENT caller), the caller must appear as either
    /// SalesAgentId or PreviousSalesAgentId on at least one row in the chain
    /// — otherwise this is someone else's appointment and the handler
    /// refuses rather than leaking who else was involved.
    /// </summary>
    public string? RestrictToAgentId { get; set; }
}
