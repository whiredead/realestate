namespace ProjectAPI.Api.Application.FinalVisits.GetFinalVisitCase;

/// <summary>
/// §17 — the internal (agent/admin) view of a reservation's final-visit case:
/// the current appointment id and the current report + snag ids, if any.
///
/// Every write endpoint in this module (SubmitReport, Acknowledge,
/// TransitionSnag) is keyed by an appointment/report/snag id that the caller
/// is expected to already know — but nothing ever returned those ids back to
/// an internal caller. The agent/admin panel had no way to learn them short
/// of guessing, so every one of those actions was unreachable through the
/// browser (N11). GetMyFinalVisitReport exists for this exact purpose but is
/// buyer-only (ownership-scoped, not project-scoped) — this is its
/// project-scoped sibling for the agent/admin caller who is submitting the
/// report and following up the snags, not receiving them.
/// </summary>
public class GetFinalVisitCaseQuery : IRequest<FinalVisitCaseDto?>
{
    public Guid ReservationId { get; set; }
}

public class FinalVisitCaseDto : Common.Units.IHasUnitLocation
{
    // Location of the unit (projet → immeuble → étage → unité).
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public Guid? ImmeubleId { get; set; }
    public string? ImmeubleName { get; set; }
    public string? FloorName { get; set; }
    public string? UnitNumber { get; set; }
    public ProjectAPI.Api.Application.Common.Units.UnitContextDto? UnitContext { get; set; }

    public Guid CaseId { get; set; }

    /// <summary>Numeric wire value of FinalVisitCaseStatus — Open=0/RevisitRequired=1/ReadyForNotary=2/Closed=3.</summary>
    public int Status { get; set; }

    /// <summary>The most recent attempt (by AttemptNo), regardless of its own status.</summary>
    /// <summary>Sales agent responsible for the visit ("agent commercial").</summary>
    public string? ResponsibleSalesAgentId { get; set; }
    public string? ResponsibleSalesAgentName { get; set; }

    public FinalVisitAppointmentDto? CurrentAppointment { get; set; }

    /// <summary>The latest report version for the current appointment, if one was submitted.</summary>
    public FinalVisitReportDto? CurrentReport { get; set; }
}

public class FinalVisitAppointmentDto
{
    public Guid AppointmentId { get; set; }
    public int AttemptNo { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string? CauseType { get; set; }
    public string? CauseDescription { get; set; }

    /// <summary>Numeric wire value of AppointmentAttemptStatus, matching finalVisitAttemptStatusCode's map.</summary>
    public int Status { get; set; }
}

public class FinalVisitReportDto
{
    public Guid ReportId { get; set; }
    public int VersionNo { get; set; }

    /// <summary>Numeric wire value of ReportStatus, matching reportStatusCode's map.</summary>
    public int Status { get; set; }

    /// <summary>Numeric wire value of VisitResult, matching visitResultCode's map.</summary>
    public int ResultCode { get; set; }
    public string? GeneralCondition { get; set; }
    public string? Observations { get; set; }
    public string? ClientFeedback { get; set; }
    public string? NonComplianceReason { get; set; }
    public string? CorrectiveAction { get; set; }
    public string? FollowUpNotes { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? DisputeReason { get; set; }
    public List<FinalVisitSnagDto> Snags { get; set; } = new();
}

public class FinalVisitSnagDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? CategoryCode { get; set; }

    /// <summary>Numeric wire value of SnagSeverity, matching snagSeverityCode's map.</summary>
    public int Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Location { get; set; }
    public DateTime? TargetResolutionDate { get; set; }

    /// <summary>Numeric wire value of SnagStatus, matching snagStatusCode's map.</summary>
    public int Status { get; set; }
}
