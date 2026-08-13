namespace ProjectAPI.Domain.Appointments.Entities;

/// <summary>
/// Sales agent's follow-up report after a commercial appointment (the visit
/// that happens between an appointment and a reservation). Mirrors
/// FinalVisitReport's shape (versioned, one report per appointment, a
/// correction creates a new version rather than editing the submitted one),
/// but for the commercial-visit stage, not the pre-delivery final visit —
/// these are unrelated reports on unrelated appointment types.
///
/// Distinct from AppointmentReview (a customer-satisfaction star-rating
/// survey with no handler wired to it at all) — this is the actual sales
/// visit report the workflow describes: what was shown, how interested the
/// buyer is, their budget/requirements, objections, and what happens next.
///
/// The customer-facing / internal split lives at the read boundary, not on
/// this entity: <see cref="InternalNotes"/> is never included in a buyer-
/// facing response (see GetAppointmentVisitReportHandler's two projections).
/// Every other field is safe to show the buyer as-is.
/// </summary>
public class AppointmentVisitReport
{
    public Guid Id { get; set; }

    public Guid AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;

    public int VersionNo { get; set; } = 1;

    /// <summary>Units actually shown during the visit (not just the ones the prospect asked about at booking — see Appointment.TypeBienIds for that).</summary>
    public List<Guid> PropertiesPresentedUnitIds { get; set; } = new();

    /// <summary>"LOW" | "MEDIUM" | "HIGH" | "VERY_HIGH".</summary>
    public string InterestLevel { get; set; } = string.Empty;

    /// <summary>Buyer-stated budget, confirmed during the visit — may differ from what they indicated at booking.</summary>
    public decimal? ConfirmedBudget { get; set; }

    /// <summary>Free text — size, layout, financing, timeline, etc.</summary>
    public string? ConfirmedRequirements { get; set; }

    /// <summary>What's holding the buyer back, if anything. Customer-facing — phrase accordingly.</summary>
    public string? Objections { get; set; }

    /// <summary>What happens next — visible to the buyer as their own next step.</summary>
    public string? NextAction { get; set; }
    public DateTime? FollowUpDate { get; set; }

    /// <summary>"INTERESTED_FOLLOW_UP" | "READY_TO_RESERVE" | "NOT_INTERESTED" | "NO_SHOW" | "RESCHEDULE_NEEDED".</summary>
    public string VisitResult { get; set; } = string.Empty;

    /// <summary>
    /// Private sales notes — pricing strategy, negotiation posture, anything
    /// not meant for the buyer to read. NEVER returned in a buyer-facing
    /// response; only visible to the agent, project admin, or global admin.
    /// </summary>
    public string? InternalNotes { get; set; }

    public string AuthorUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>§31.6 — optimistic concurrency; the agent and an admin could act near-simultaneously.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>Buyer-stated interest level after the visit.</summary>
public static class VisitInterestLevelCodes
{
    public const string Low = "LOW";
    public const string Medium = "MEDIUM";
    public const string High = "HIGH";
    public const string VeryHigh = "VERY_HIGH";

    public static readonly string[] All = { Low, Medium, High, VeryHigh };
}

/// <summary>Pipeline outcome of the visit — distinct from FinalVisits.VisitResult, which is about property compliance, not sales-pipeline stage.</summary>
public static class AppointmentVisitResultCodes
{
    public const string InterestedFollowUp = "INTERESTED_FOLLOW_UP";
    public const string ReadyToReserve = "READY_TO_RESERVE";
    public const string NotInterested = "NOT_INTERESTED";
    public const string NoShow = "NO_SHOW";
    public const string RescheduleNeeded = "RESCHEDULE_NEEDED";

    public static readonly string[] All = { InterestedFollowUp, ReadyToReserve, NotInterested, NoShow, RescheduleNeeded };
}
