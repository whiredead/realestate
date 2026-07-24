namespace ProjectAPI.Domain.FinalVisits.Entities;

/// <summary>
/// Final-visit dossier for a reservation (spec §17.1, §48.8).
///
/// There is exactly ONE case per reservation. Rescheduling adds a new attempt
/// inside the same case rather than overwriting the first visit (§17.1
/// FR-FVI-001), so the full history of attempts, reports and snags is kept.
/// </summary>
public class FinalVisitCase
{
    public Guid Id { get; set; }

    /// <summary>Unique: one case per reservation.</summary>
    public Guid ReservationId { get; set; }

    public FinalVisitCaseStatus Status { get; set; } = FinalVisitCaseStatus.Open;

    /// <summary>Sales agent responsible for the visit and its snags (§17.4 FR-FVI-010).</summary>
    public string? ResponsibleSalesAgentId { get; set; }

    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    public ICollection<FinalVisitAppointment> Appointments { get; set; } = new List<FinalVisitAppointment>();
}

/// <summary>Dossier-level state (§17.5).</summary>
public enum FinalVisitCaseStatus
{
    Open = 0,
    /// <summary>A further visit is needed before the notary stage.</summary>
    RevisitRequired = 1,
    /// <summary>Cleared for the notary appointment.</summary>
    ReadyForNotary = 2,
    Closed = 3
}

/// <summary>
/// One attempt at a final visit (spec §17.5, §48.8).
/// Attempts are numbered within their case and chained through
/// <see cref="PreviousAppointmentId"/>.
/// </summary>
public class FinalVisitAppointment
{
    public Guid Id { get; set; }

    public Guid CaseId { get; set; }
    public FinalVisitCase Case { get; set; } = null!;

    /// <summary>1-based attempt number within the case.</summary>
    public int AttemptNo { get; set; }

    /// <summary>Half-open interval [StartsAt, EndsAt) per §10.1.</summary>
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    public AppointmentAttemptStatus Status { get; set; } = AppointmentAttemptStatus.Requested;

    /// <summary>Previous attempt, when this one follows a reschedule.</summary>
    public Guid? PreviousAppointmentId { get; set; }

    /// <summary>Why a new attempt was needed (§17.2 FR-FVI-004).</summary>
    public string? CauseType { get; set; }
    public string? CauseDescription { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Shared appointment lifecycle (§47.3). The same matrix governs commercial,
/// final-visit and notary appointments; only the permissions differ.
/// </summary>
public enum AppointmentAttemptStatus
{
    Requested = 0,
    Confirmed = 1,
    RescheduleProposed = 2,
    Rejected = 3,
    Cancelled = 4,
    Completed = 5,
    NoShow = 6
}

/// <summary>Transition rules shared by every appointment type (§47.3).</summary>
public static class AppointmentStateMachine
{
    private static readonly IReadOnlyDictionary<AppointmentAttemptStatus, AppointmentAttemptStatus[]> Allowed =
        new Dictionary<AppointmentAttemptStatus, AppointmentAttemptStatus[]>
        {
            [AppointmentAttemptStatus.Requested] = new[]
            {
                AppointmentAttemptStatus.Confirmed,
                AppointmentAttemptStatus.RescheduleProposed,
                AppointmentAttemptStatus.Rejected,
                AppointmentAttemptStatus.Cancelled
            },
            [AppointmentAttemptStatus.RescheduleProposed] = new[]
            {
                AppointmentAttemptStatus.Confirmed,
                AppointmentAttemptStatus.Requested,
                AppointmentAttemptStatus.Cancelled
            },
            [AppointmentAttemptStatus.Confirmed] = new[]
            {
                AppointmentAttemptStatus.Completed,
                AppointmentAttemptStatus.Cancelled,
                AppointmentAttemptStatus.NoShow
            },
            // Terminal states (§10.3): a retry creates a NEW attempt.
            [AppointmentAttemptStatus.Rejected] = Array.Empty<AppointmentAttemptStatus>(),
            [AppointmentAttemptStatus.Cancelled] = Array.Empty<AppointmentAttemptStatus>(),
            [AppointmentAttemptStatus.Completed] = Array.Empty<AppointmentAttemptStatus>(),
            [AppointmentAttemptStatus.NoShow] = Array.Empty<AppointmentAttemptStatus>()
        };

    public static bool CanTransition(AppointmentAttemptStatus from, AppointmentAttemptStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>Statuses that occupy an agent's slot and must not overlap.</summary>
    public static readonly AppointmentAttemptStatus[] BlockingStatuses =
    {
        AppointmentAttemptStatus.Requested,
        AppointmentAttemptStatus.Confirmed,
        AppointmentAttemptStatus.RescheduleProposed
    };
}
