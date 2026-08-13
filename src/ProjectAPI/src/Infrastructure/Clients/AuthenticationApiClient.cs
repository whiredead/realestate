using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Settings;

namespace ProjectAPI.Infrastructure.Clients;

/// <summary>Wire shape of AuthenticationAPI's ProvisionInternalUserCommand/Response — kept local rather than shared, same convention as AuthenticationAPI's own ProvisionUserRequest.</summary>
internal record ProvisionInternalUserWireRequest(string Email, string FirstName, string LastName, string Password, string RoleCode, string? PhoneNumber);
internal record ProvisionInternalUserWireResponse(string UserId, string Email, bool WasExistingAccount, IReadOnlyList<string> RolesAfter);

public class AuthenticationApiClient : IAuthenticationApiClient
{
    private const string HeaderName = "X-Internal-Api-Key";

    private readonly HttpClient _httpClient;
    private readonly InternalApiSettings _settings;
    private readonly ILogger<AuthenticationApiClient> _logger;

    public AuthenticationApiClient(HttpClient httpClient, InternalApiSettings settings, ILogger<AuthenticationApiClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
    }

    public async Task<ProvisionBuyerResponse> ProvisionBuyerAsync(ProvisionBuyerRequest request, CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/internal/Internal/users/provision")
        {
            Content = JsonContent.Create(new ProvisionInternalUserWireRequest(
                request.Email, request.FirstName, request.LastName, request.Password, RoleCodes.Buyer, request.PhoneNumber))
        };
        message.Headers.Add(HeaderName, _settings.ApiKey);

        // Unlike ProjectApiClient.ProvisionUserAsync (fire-and-forget, mirrors
        // an account that already exists elsewhere), this call's result IS the
        // account: if AuthenticationAPI is unreachable or refuses, the
        // invitation must not be marked accepted and no CrmContact.UserId link
        // should be written — the caller propagates the failure rather than
        // swallowing it.
        var response = await _httpClient.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "[AuthenticationApiClient] Failed to provision BUYER account for {Email}: {Status} {Body}",
                request.Email, response.StatusCode, body);
            throw new InvalidOperationException($"Could not create account ({response.StatusCode}).");
        }

        var wire = await response.Content.ReadFromJsonAsync<ProvisionInternalUserWireResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("AuthenticationAPI returned an empty provisioning response.");

        return new ProvisionBuyerResponse(wire.UserId, wire.Email, wire.WasExistingAccount);
    }
}
