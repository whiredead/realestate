using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ProjectAPI.Api.Hubs;

/// <summary>
/// Authenticated, server-to-client notification channel. Notifications are
/// persisted before they are published, so this hub exposes no business operations.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub;

/// <summary>Matches SignalR users to the custom userId claim emitted by TokenProvider.</summary>
public sealed class NotificationUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst("userId")?.Value
        ?? connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
