using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ProjectAPI.Api.Hubs;

/// <summary>Authenticated, per-user notification channel.</summary>
[Authorize]
public sealed class NotificationHub : Hub
{
}

/// <summary>Maps this application's custom JWT user id claim to SignalR users.</summary>
public sealed class NotificationUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst("userId")?.Value;
}
