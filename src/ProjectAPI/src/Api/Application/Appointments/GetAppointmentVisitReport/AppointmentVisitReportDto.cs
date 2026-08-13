namespace ProjectAPI.Api.Application.Appointments.GetAppointmentVisitReport;

/// <summary>Full internal view — agent/admin only. Includes InternalNotes.</summary>
public class AppointmentVisitReportDto
{
    public Guid Id { get; set; }
    public Guid AppointmentId { get; set; }
    public int VersionNo { get; set; }
    public List<Guid> PropertiesPresentedUnitIds { get; set; } = new();
    public string InterestLevel { get; set; } = string.Empty;
    public decimal? ConfirmedBudget { get; set; }
    public string? ConfirmedRequirements { get; set; }
    public string? Objections { get; set; }
    public string? NextAction { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public string VisitResult { get; set; } = string.Empty;
    public string? InternalNotes { get; set; }
    public string AuthorUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Buyer-facing summary — deliberately omits InternalNotes, ConfirmedBudget,
/// Objections and AuthorUserId: what the buyer needs is "what we saw and
/// what happens next," not the agent's internal read on their negotiating
/// position or private notes.
/// </summary>
public class AppointmentVisitReportBuyerDto
{
    public Guid Id { get; set; }
    public int VersionNo { get; set; }
    public List<Guid> PropertiesPresentedUnitIds { get; set; } = new();
    public string? NextAction { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
