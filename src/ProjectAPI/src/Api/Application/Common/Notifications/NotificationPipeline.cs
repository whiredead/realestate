using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Notifications.GetMyNotifications;
using ProjectAPI.Api.Hubs;
using ProjectAPI.Domain.Notifications.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Common.Notifications;

public sealed record NotificationMessage(
    string Type,
    string TitleFr,
    string BodyFr,
    Guid? RelatedEntityId = null,
    string? RelatedEntityType = null,
    string? TitleEn = null,
    string? BodyEn = null);

/// <summary>Resolved to concrete users before filtering, persistence, and delivery.</summary>
public abstract record NotificationAudience
{
    public sealed record Users(IReadOnlyCollection<string> UserIds) : NotificationAudience;
    public sealed record GlobalRole(string RoleCode) : NotificationAudience;
    public sealed record ProjectRole(Guid ProjectId, string RoleCode) : NotificationAudience;

    public static NotificationAudience ForUser(string userId) => new Users(new[] { userId });
    public static NotificationAudience ForUsers(params string[] userIds) => new Users(userIds);
    public static NotificationAudience ForRole(string roleCode) => new GlobalRole(roleCode);
    public static NotificationAudience ForProjectRole(Guid projectId, string roleCode) =>
        new ProjectRole(projectId, roleCode);
}

public sealed record NotificationFilterContext(NotificationMessage Message, string UserId);

/// <summary>All applicable filters must accept a notification for its recipient.</summary>
public interface INotificationFilter
{
    int Order { get; }
    bool AppliesTo(NotificationMessage message);
    Task<bool> CanDeliverAsync(NotificationFilterContext context, CancellationToken ct);
}

/// <summary>Preserves all existing notification behavior until specific filters are added.</summary>
public sealed class AllowAllNotificationFilter : INotificationFilter
{
    public int Order => int.MaxValue;
    public bool AppliesTo(NotificationMessage message) => true;
    public Task<bool> CanDeliverAsync(NotificationFilterContext context, CancellationToken ct) =>
        Task.FromResult(true);
}

public interface INotificationRecipientResolver
{
    Task<IReadOnlyCollection<string>> ResolveAsync(NotificationAudience audience, CancellationToken ct);
}

public sealed class NotificationRecipientResolver : INotificationRecipientResolver
{
    private readonly ApplicationDbContext _db;

    public NotificationRecipientResolver(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyCollection<string>> ResolveAsync(
        NotificationAudience audience,
        CancellationToken ct)
    {
        IEnumerable<string> recipients = audience switch
        {
            NotificationAudience.Users users => users.UserIds,
            NotificationAudience.GlobalRole role => await ResolveGlobalRoleAsync(role.RoleCode, ct),
            NotificationAudience.ProjectRole role => await ResolveProjectRoleAsync(role, ct),
            _ => Array.Empty<string>()
        };

        return recipients
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<IReadOnlyCollection<string>> ResolveGlobalRoleAsync(string roleCode, CancellationToken ct)
    {
        var wanted = RoleCodes.Normalize(roleCode);
        var assignments = await _db.UserRoles
            .AsNoTracking()
            .Select(userRole => new { userRole.UserId, RoleName = userRole.Role.Name })
            .ToListAsync(ct);

        return assignments
            .Where(x => RoleCodes.Normalize(x.RoleName) == wanted)
            .Select(x => x.UserId)
            .ToArray();
    }

    private async Task<IReadOnlyCollection<string>> ResolveProjectRoleAsync(
        NotificationAudience.ProjectRole audience,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var roleCode = RoleCodes.Normalize(audience.RoleCode);
        return await _db.Set<ProjectMembership>()
            .AsNoTracking()
            .Where(m => m.ProjectId == audience.ProjectId
                && m.RoleCode == roleCode
                && m.IsActive
                && m.ValidFrom <= now
                && (m.ValidUntil == null || m.ValidUntil > now))
            .Select(m => m.UserId)
            .Distinct()
            .ToArrayAsync(ct);
    }
}

public interface INotificationRealtimePublisher
{
    Task PublishAsync(Notification notification, CancellationToken ct);
}

public sealed class SignalRNotificationPublisher : INotificationRealtimePublisher
{
    private readonly IHubContext<NotificationHub> _hub;

    public SignalRNotificationPublisher(IHubContext<NotificationHub> hub) => _hub = hub;

    public Task PublishAsync(Notification notification, CancellationToken ct) =>
        _hub.Clients.User(notification.UserId).SendAsync(
            "ReceiveNotification",
            new NotificationDto
            {
                Id = notification.Id,
                Type = notification.Type,
                Title = notification.TitleFr,
                Body = notification.BodyFr,
                RelatedEntityId = notification.RelatedEntityId,
                RelatedEntityType = notification.RelatedEntityType,
                IsRead = notification.IsRead,
                CreatedAt = notification.CreatedAt
            },
            ct);
}
