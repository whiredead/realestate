using ProjectAPI.Domain.FinalVisits.Entities;

namespace ProjectAPI.Domain.Handovers.Entities;

/// <summary>
/// Remise des clés — spec §5.8 / §19.
///
/// Keyed on the RESERVATION, not on the legacy <c>Sale</c>: a sale is not an
/// entity in the spec, it is a reservation that reached CONVERTED (§5.3/§5.7).
/// The previous <c>PropertyDelivery</c> hung off <c>SaleId</c> with a free-text
/// status and a single report string, so nothing could express the one step that
/// matters — the buyer confirming they received the property.
///
/// Uses the shared appointment machine (§47.3), the same one final-visit and
/// notary appointments follow. A terminal attempt is never reopened; a retry is a
/// new row pointing back through <see cref="PreviousAppointmentId"/>.
/// </summary>
public class HandoverAppointment
{
    public Guid Id { get; set; }

    public Guid ReservationId { get; set; }

    public Guid UnitId { get; set; }

    public DateTime ScheduledAt { get; set; }

    public DateTime? EndsAt { get; set; }

    public string? Location { get; set; }

    /// <summary>Agent conducting the handover.</summary>
    public string? SalesAgentId { get; set; }

    public AppointmentAttemptStatus Status { get; set; } = AppointmentAttemptStatus.Requested;

    /// <summary>Set when this attempt replaces a terminal one (§5.1).</summary>
    public Guid? PreviousAppointmentId { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; }

    public ICollection<HandoverReport> Reports { get; set; } = new List<HandoverReport>();
}

/// <summary>
/// Procès-verbal de livraison (§19.3). Versioned: a correction produces a new
/// version rather than overwriting a document the buyer already acknowledged.
/// </summary>
public class HandoverReport
{
    public Guid Id { get; set; }

    public Guid AppointmentId { get; set; }

    public HandoverAppointment Appointment { get; set; } = null!;

    public int VersionNo { get; set; } = 1;

    public HandoverReportStatus Status { get; set; } = HandoverReportStatus.Draft;

    /// <summary>People actually present at the handover.</summary>
    public string? Participants { get; set; }

    /// <summary>Meter readings, general condition, anything noted on the day.</summary>
    public string? Observations { get; set; }

    public DateTime? SubmittedAt { get; set; }

    /// <summary>
    /// The moment the buyer confirmed receipt. This — not the appointment being
    /// marked completed — is what delivers the property (§5.8).
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>
    /// Who confirmed. A contact id, not a user id: a buyer with no login still
    /// takes delivery (§1.1), and the agent records their confirmation.
    /// </summary>
    public Guid? AcknowledgedByContactId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<HandoverItem> Items { get; set; } = new List<HandoverItem>();
}

/// <summary>What was physically handed over — keys, badges, remote controls (§19.3).</summary>
public class HandoverItem
{
    public Guid Id { get; set; }

    public Guid ReportId { get; set; }

    public HandoverReport Report { get; set; } = null!;

    public string ItemType { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string? Comment { get; set; }
}

public enum HandoverReportStatus
{
    Draft = 0,
    Submitted = 1,
    AwaitingBuyerAcknowledgement = 2,
    Acknowledged = 3,
    Superseded = 4,
}

public static class HandoverReportStatusCodes
{
    public const string Draft = "DRAFT";
    public const string Submitted = "SUBMITTED";
    public const string AwaitingBuyerAcknowledgement = "AWAITING_BUYER_ACKNOWLEDGEMENT";
    public const string Acknowledged = "ACKNOWLEDGED";
    public const string Superseded = "SUPERSEDED";

    public static readonly string[] All =
    {
        Draft, Submitted, AwaitingBuyerAcknowledgement, Acknowledged, Superseded
    };

    public static string ToCode(this HandoverReportStatus s) => s switch
    {
        HandoverReportStatus.Draft => Draft,
        HandoverReportStatus.Submitted => Submitted,
        HandoverReportStatus.AwaitingBuyerAcknowledgement => AwaitingBuyerAcknowledgement,
        HandoverReportStatus.Acknowledged => Acknowledged,
        HandoverReportStatus.Superseded => Superseded,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, "Unknown handover report status."),
    };

    public static HandoverReportStatus Parse(string? v) => v?.Trim().ToUpperInvariant() switch
    {
        Submitted => HandoverReportStatus.Submitted,
        AwaitingBuyerAcknowledgement => HandoverReportStatus.AwaitingBuyerAcknowledgement,
        Acknowledged => HandoverReportStatus.Acknowledged,
        Superseded => HandoverReportStatus.Superseded,
        _ => HandoverReportStatus.Draft,
    };
}

/// <summary>
/// Garantie (§5.9 / §20). Created by delivery, never before: a warranty period
/// that has not started cannot be claimed against, which is precisely the gate
/// SAV needs ("claims only for DELIVERED units").
/// </summary>
public class Warranty
{
    public Guid Id { get; set; }

    public Guid UnitId { get; set; }

    public Guid ReservationId { get; set; }

    /// <summary>Referential code — structural, waterproofing, equipment…</summary>
    public string WarrantyTypeCode { get; set; } = "GENERAL";

    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
