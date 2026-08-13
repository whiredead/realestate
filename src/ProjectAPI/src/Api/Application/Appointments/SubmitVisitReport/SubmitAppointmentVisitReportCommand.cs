namespace ProjectAPI.Api.Application.Appointments.SubmitVisitReport;

/// <summary>
/// The sales agent's follow-up report after a commercial appointment's visit.
/// Always creates a new AppointmentVisitReport row — a correction is a new
/// version, never an edit of a submitted one (mirrors FinalVisitReport).
/// </summary>
public class SubmitAppointmentVisitReportCommand : IRequest<SubmitAppointmentVisitReportResponse>
{
    public Guid AppointmentId { get; set; }
    public List<Guid> PropertiesPresentedUnitIds { get; set; } = new();

    /// <summary>"LOW" | "MEDIUM" | "HIGH" | "VERY_HIGH".</summary>
    public required string InterestLevel { get; set; }

    public decimal? ConfirmedBudget { get; set; }
    public string? ConfirmedRequirements { get; set; }
    public string? Objections { get; set; }
    public string? NextAction { get; set; }
    public DateTime? FollowUpDate { get; set; }

    /// <summary>"INTERESTED_FOLLOW_UP" | "READY_TO_RESERVE" | "NOT_INTERESTED" | "NO_SHOW" | "RESCHEDULE_NEEDED".</summary>
    public required string VisitResult { get; set; }

    /// <summary>Private — never shown to the buyer.</summary>
    public string? InternalNotes { get; set; }

    public string? AuthorUserId { get; set; }
}
