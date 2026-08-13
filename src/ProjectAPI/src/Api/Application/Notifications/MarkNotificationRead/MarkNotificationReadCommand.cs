namespace ProjectAPI.Api.Application.Notifications.MarkNotificationRead;

public class MarkNotificationReadCommand : IRequest<bool>
{
    public Guid NotificationId { get; set; }
}
