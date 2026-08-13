namespace ProjectAPI.Api.Application.Handovers.GetMyHandoverStatus;

/// <summary>
/// §5.8/§19/§8 — the buyer's own handover status for one of their reservations.
/// Ownership is proven through Reservation.BuyerId (Handover* rows carry no
/// buyer id of their own), matching GetAppointmentVisitReportForBuyerHandler's
/// 403-not-404 pattern for a reservation that isn't the caller's.
/// </summary>
public class GetMyHandoverStatusQuery : IRequest<MyHandoverStatusDto?>
{
    public Guid ReservationId { get; set; }
}

public class MyHandoverStatusDto
{
    public Guid AppointmentId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public string? Location { get; set; }

    /// <summary>Numeric wire value of AppointmentAttemptStatus (§47.3 shared machine), matching finalVisitsApi's finalVisitAttemptStatusCode map. Superseded=7 is the one value that map doesn't cover.</summary>
    public int AppointmentStatus { get; set; }

    public Guid? ReportId { get; set; }
    public int? ReportVersionNo { get; set; }

    /// <summary>Numeric wire value of HandoverReportStatus — Draft=0/Submitted=1/AwaitingBuyerAcknowledgement=2/Acknowledged=3/Superseded=4.</summary>
    public int? ReportStatus { get; set; }
    public DateTime? ReportSubmittedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public List<MyHandoverItemDto> Items { get; set; } = new();
}

public class MyHandoverItemDto
{
    public string ItemType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Comment { get; set; }
}
