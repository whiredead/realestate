using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Notifications.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Notifications.MarkNotificationRead;

/// <summary>A user may only mark their OWN notifications read.</summary>
public class MarkNotificationReadHandler : IRequestHandler<MarkNotificationReadCommand, bool>
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public MarkNotificationReadHandler(ApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        var notification = await _db.Set<Notification>()
            .FirstOrDefaultAsync(n => n.Id == request.NotificationId, ct)
            ?? throw new NotFoundException($"Notification {request.NotificationId} not found.");

        if (notification.UserId != _currentUser.UserId)
        {
            throw BusinessRuleException.ProjectScopeDenied(Guid.Empty);
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return true;
    }
}
