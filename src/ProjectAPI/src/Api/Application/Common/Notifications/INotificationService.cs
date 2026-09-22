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

    Task NotifyAsync(
        NotificationMessage message,
        NotificationAudience audience,
        CancellationToken ct = default);
}

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationRecipientResolver _recipients;
    private readonly IReadOnlyCollection<INotificationFilter> _filters;
    private readonly INotificationRealtimePublisher _publisher;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ApplicationDbContext db,
        INotificationRecipientResolver recipients,
        IEnumerable<INotificationFilter> filters,
        INotificationRealtimePublisher publisher,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _recipients = recipients;
        _filters = filters.OrderBy(filter => filter.Order).ToArray();
        _publisher = publisher;
        _logger = logger;
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

        await NotifyAsync(
            new NotificationMessage(
                type,
                titleFr,
                bodyFr,
                relatedEntityId,
                relatedEntityType),
            NotificationAudience.ForUser(userId),
            ct);
    }

    public async Task NotifyAsync(
        NotificationMessage message,
        NotificationAudience audience,
        CancellationToken ct = default)
    {
        var userIds = await _recipients.ResolveAsync(audience, ct);
        var notifications = new List<Notification>();

        foreach (var userId in userIds)
        {
            var context = new NotificationFilterContext(message, userId);
            var accepted = true;
            foreach (var filter in _filters)
            {
                if (filter.AppliesTo(message) && !await filter.CanDeliverAsync(context, ct))
                {
                    accepted = false;
                    break;
                }
            }

            if (!accepted) continue;

            notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = message.Type,
                TitleFr = message.TitleFr,
                TitleEn = message.TitleEn,
                BodyFr = message.BodyFr,
                BodyEn = message.BodyEn,
                RelatedEntityId = message.RelatedEntityId,
                RelatedEntityType = message.RelatedEntityType,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (notifications.Count == 0) return;

        _db.AddRange(notifications);
        await _db.SaveChangesAsync(ct);

        foreach (var notification in notifications)
        {
            try
            {
                await _publisher.PublishAsync(notification, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Notification {NotificationId} was persisted but realtime delivery to user {UserId} failed.",
                    notification.Id,
                    notification.UserId);
            }
        }
    }
}
