namespace ProjectAPI.Api.Application.FinalVisits.GetMyFinalVisitReport;

/// <summary>
/// §17.3/§8 — the buyer's own final-visit report and its reserves (snags), for
/// one of their reservations. Ownership proven through Reservation.BuyerId,
/// same pattern as GetAppointmentVisitReportForBuyerHandler. Omits
/// ResponsibleSalesAgentId, ResolutionComment, ProofUrl and AuthorUserId — those
/// are the agent's internal follow-up, not what the buyer needs to review or
/// dispute the report (§17.3 FR-FVI-007).
/// </summary>
public class GetMyFinalVisitReportQuery : IRequest<MyFinalVisitReportDto?>
{
    public Guid ReservationId { get; set; }
}

public class MyFinalVisitReportDto
{
    public Guid ReportId { get; set; }
    public int VersionNo { get; set; }

    /// <summary>Numeric wire value of ReportStatus — Draft=0/Submitted=1/AwaitingBuyerAcknowledgement=2/Acknowledged=3/Disputed=4/Superseded=5.</summary>
    public int Status { get; set; }

    /// <summary>Numeric wire value of VisitResult, matching finalVisitsApi's visitResultCode map.</summary>
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
    public List<MySnagDto> Snags { get; set; } = new();
}

public class MySnagDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? CategoryCode { get; set; }

    /// <summary>Numeric wire value of SnagSeverity, matching finalVisitsApi's snagSeverityCode map.</summary>
    public int Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Location { get; set; }
    public DateTime? TargetResolutionDate { get; set; }

    /// <summary>Numeric wire value of SnagStatus, matching finalVisitsApi's snagStatusCode map.</summary>
    public int Status { get; set; }
}
