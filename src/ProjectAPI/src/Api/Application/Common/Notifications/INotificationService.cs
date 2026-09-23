using Microsoft.AspNetCore.SignalR;
using ProjectAPI.Api.Application.Notifications.GetMyNotifications;
using ProjectAPI.Api.Hubs;
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
    private readonly IHubContext<NotificationHub> _hub;

    public NotificationService(ApplicationDbContext db, IHubContext<NotificationHub> hub)
    {
        _db = db;
        _hub = hub;
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

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            TitleFr = titleFr,
            BodyFr = bodyFr,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType,
            CreatedAt = DateTime.UtcNow
        };
        _db.Add(notification);

        await _db.SaveChangesAsync(ct);

        await _hub.Clients.User(userId).SendAsync("ReceiveNotification", new NotificationDto
        {
            Id = notification.Id,
            Type = notification.Type,
            Title = notification.TitleFr,
            Body = notification.BodyFr,
            RelatedEntityId = notification.RelatedEntityId,
            RelatedEntityType = notification.RelatedEntityType,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt,
        }, ct);
    }
}
