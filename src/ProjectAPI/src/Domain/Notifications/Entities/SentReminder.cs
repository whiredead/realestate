namespace ProjectAPI.Domain.Notifications.Entities;

/// <summary>
/// §7 — "appointment reminders (5 min)" job's own idempotence marker. One row
/// per (EntityType, EntityId) that already got its reminder, so a job that
/// runs every 5 minutes does not re-notify the same upcoming appointment on
/// every pass. Kept separate from the four appointment entities (commercial,
/// final-visit, notary, handover) rather than adding a ReminderSentAt column
/// to each — one small table instead of four schema changes for a single
/// scheduling concern.
/// </summary>
public class SentReminder
{
    public Guid Id { get; set; }

    /// <summary>"CommercialAppointment", "FinalVisitAppointment", "NotaryAppointment", "HandoverAppointment".</summary>
    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
