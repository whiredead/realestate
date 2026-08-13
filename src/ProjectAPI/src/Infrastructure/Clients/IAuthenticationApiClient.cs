namespace ProjectAPI.Infrastructure.Clients;

public record ProvisionBuyerRequest(string Email, string FirstName, string LastName, string Password, string? PhoneNumber);

public record ProvisionBuyerResponse(string UserId, string Email, bool WasExistingAccount);

/// <summary>
/// Calls AuthenticationAPI's own internal endpoints — the reverse direction
/// of AuthenticationAPI's IProjectApiClient. AuthenticationAPI is the source
/// of truth for accounts; ProjectAPI never creates an AspNetUsers row
/// directly, even for the Phase 2 invitation-acceptance flow this client
/// exists for (AcceptInvitationHandler).
/// </summary>
public interface IAuthenticationApiClient
{
    /// <summary>
    /// Creates (or, if an account with this email already exists, adds the
    /// BUYER role to) the account, unlike ProjectApiClient.ProvisionUserAsync
    /// this is NOT fire-and-forget: the caller needs the real UserId back to
    /// link CrmContact.UserId and mark the invitation accepted.
    /// </summary>
    Task<ProvisionBuyerResponse> ProvisionBuyerAsync(ProvisionBuyerRequest request, CancellationToken ct = default);
}
