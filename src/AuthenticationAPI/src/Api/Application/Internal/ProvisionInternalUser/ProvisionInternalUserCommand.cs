namespace AuthenticationAPI.Api.Application.Internal.ProvisionInternalUser;

/// <summary>
/// Phase 2 — issued only by ProjectAPI's AcceptInternalInvitationHandler via
/// the internal API-key-protected endpoint. Never reachable from public
/// registration: RegisterHandler stays PROSPECT-only. This is the one other
/// place besides RegisterHandler that may call CreateAsync/AddToRolesAsync
/// for an account.
/// </summary>
public class ProvisionInternalUserCommand : IRequest<ProvisionInternalUserResponse>
{
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }

    /// <summary>Only used when creating a brand-new account. An existing account keeps its own password — see ProvisionInternalUserHandler.</summary>
    public required string Password { get; init; }

    /// <summary>Canonical §6.1 code — already validated as invite-eligible by ProjectAPI before this call.</summary>
    public required string RoleCode { get; init; }

    public string? PhoneNumber { get; init; }
}
