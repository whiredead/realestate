using ProjectAPI.Domain.Notifications.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Common.Notifications;

/// <summary>
/// §6.2/§7 — "notifications after each event" and the three scheduled jobs
/// (appointment reminders, installment refresh, SAV SLA detection) all funnel
/// through here so notification creation is uniform rather than each job
/// writing its own Notification row shape.
/// </summary>
public interface INotificationService
{
    Task NotifyAsync(
        string userId,
        string type,
        string titleFr,
        string bodyFr,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null,
        CancellationToken ct = default);
}

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;

    public NotificationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task NotifyAsync(
        string userId,
        string type,
        string titleFr,
        string bodyFr,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        _db.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            TitleFr = titleFr,
            BodyFr = bodyFr,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }
}
