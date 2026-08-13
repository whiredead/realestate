namespace ProjectAPI.Domain.Notifications.Entities;

/// <summary>
/// §6.2 "notifications (per user)" — a transactional, in-app notification.
/// Distinct from marketing communications (§5.10/§21): these are system
/// events (appointment reminders, overdue installments, SLA breaches, status
/// changes), never subject to marketing consent/opt-out.
/// </summary>
public class Notification
{
    public Guid Id { get; set; }

    /// <summary>AspNetUsers.Id of the recipient.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Stable code identifying the event, e.g. "APPOINTMENT_REMINDER", "INSTALLMENT_OVERDUE".</summary>
    public string Type { get; set; } = string.Empty;

    public string TitleFr { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public string BodyFr { get; set; } = string.Empty;
    public string? BodyEn { get; set; }

    /// <summary>Optional deep link target, e.g. a reservation or claim id, for the client to route to.</summary>
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
