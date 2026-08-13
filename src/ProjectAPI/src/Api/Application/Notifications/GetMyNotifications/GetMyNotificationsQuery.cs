namespace ProjectAPI.Api.Application.Notifications.GetMyNotifications;

/// <summary>§8 "Notifications" — always the caller's own, never a supplied user id.</summary>
public class GetMyNotificationsQuery : IRequest<List<NotificationDto>>
{
    public bool UnreadOnly { get; set; }
}

public class NotificationDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
