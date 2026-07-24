namespace ProjectAPI.Domain.Construction.Entities;

/// <summary>
/// Current land-title status of a unit (spec §16, §48.7).
/// Exactly one current row per unit; every change appends to
/// <see cref="UnitTitleHistory"/>.
/// </summary>
public class UnitTitleState
{
    public Guid Id { get; set; }

    public Guid UnitId { get; set; }

    public TitleStatus Status { get; set; } = TitleStatus.NotAvailable;

    public DateTime StatusAt { get; set; } = DateTime.UtcNow;

    /// <summary>Supporting document, when one exists.</summary>
    public string? DocumentUrl { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Append-only trace of title changes (§16 FR-TITLE-001).</summary>
public class UnitTitleHistory
{
    public Guid Id { get; set; }

    public Guid UnitId { get; set; }

    public TitleStatus? FromStatus { get; set; }
    public TitleStatus ToStatus { get; set; }

    public string? ActorUserId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    /// <summary>Mandatory when moving backwards (§16).</summary>
    public string? Reason { get; set; }

    public string? DocumentUrl { get; set; }
}

/// <summary>Title lifecycle (§16 FR-TITLE-001).</summary>
public enum TitleStatus
{
    NotAvailable = 0,
    InProgress = 1,
    Available = 2,
    DeliveredToNotary = 3,
    Completed = 4
}

/// <summary>
/// Title transition rules (§16).
///
/// Forward path: NOT_AVAILABLE → IN_PROGRESS → AVAILABLE → DELIVERED_TO_NOTARY
/// → COMPLETED. Moving backwards is allowed but requires a reason and an
/// administrator, so it is deliberately modelled rather than forbidden.
/// </summary>
public static class TitleStateMachine
{
    private static readonly Dictionary<TitleStatus, TitleStatus> Forward = new()
    {
        [TitleStatus.NotAvailable] = TitleStatus.InProgress,
        [TitleStatus.InProgress] = TitleStatus.Available,
        [TitleStatus.Available] = TitleStatus.DeliveredToNotary,
        [TitleStatus.DeliveredToNotary] = TitleStatus.Completed
    };

    /// <summary>True when the move follows the normal forward path.</summary>
    public static bool IsForward(TitleStatus from, TitleStatus to) =>
        Forward.TryGetValue(from, out var next) && next == to;

    /// <summary>True when the move goes backwards, which requires a reason.</summary>
    public static bool IsBackward(TitleStatus from, TitleStatus to) => to < from;

    /// <summary>
    /// Title states that satisfy the notary prerequisite (§18.3 FR-NOT-002):
    /// AVAILABLE, DELIVERED_TO_NOTARY or COMPLETED.
    /// </summary>
    public static bool AllowsNotaryAppointment(TitleStatus status) =>
        status is TitleStatus.Available or TitleStatus.DeliveredToNotary or TitleStatus.Completed;
}
