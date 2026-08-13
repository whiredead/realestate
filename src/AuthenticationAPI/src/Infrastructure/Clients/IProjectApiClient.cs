namespace AuthenticationAPI.Infrastructure.Clients;

public record ProvisionUserRequest(string Id, string Email, string FirstName, string LastName, string? PhoneNumber, string Role);

/// <summary>
/// Calls ProjectAPI's own internal endpoints — the reverse direction of
/// IAuthenticationApiClient (ProjectAPI → AuthenticationAPI, used by the
/// Phase 2 invitation flow). AuthenticationAPI is the source of truth for
/// accounts, but ProjectAPI keeps its own AspNetUsers table for local FKs
/// (Appointment.SalesAgentId, ProjectMembership.UserId, etc.) — nothing else
/// keeps the two in sync, so any handler that creates an internal-role
/// account here must also call ProvisionUserAsync.
/// </summary>
public interface IProjectApiClient
{
    Task ProvisionUserAsync(ProvisionUserRequest request, CancellationToken ct = default);
}
