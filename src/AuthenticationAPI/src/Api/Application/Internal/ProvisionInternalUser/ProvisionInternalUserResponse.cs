namespace AuthenticationAPI.Api.Application.Internal.ProvisionInternalUser;

public class ProvisionInternalUserResponse
{
    public required string UserId { get; init; }
    public required string Email { get; init; }

    /// <summary>True if this call found and reused an existing account (attached a role) rather than creating one.</summary>
    public required bool WasExistingAccount { get; init; }

    public required IReadOnlyList<string> RolesAfter { get; init; }
}
