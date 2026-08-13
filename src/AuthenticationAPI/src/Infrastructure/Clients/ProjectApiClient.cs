using System.Net.Http.Json;
using AuthenticationAPI.Infrastructure.Settings;
using Microsoft.Extensions.Logging;

namespace AuthenticationAPI.Infrastructure.Clients;

public class ProjectApiClient : IProjectApiClient
{
    private const string HeaderName = "X-Internal-Api-Key";

    private readonly HttpClient _httpClient;
    private readonly InternalApiSettings _settings;
    private readonly ILogger<ProjectApiClient> _logger;

    public ProjectApiClient(HttpClient httpClient, InternalApiSettings settings, ILogger<ProjectApiClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
    }

    public async Task ProvisionUserAsync(ProvisionUserRequest request, CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/internal/Internal/users/provision")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(HeaderName, _settings.ApiKey);

        // Non-fatal by design: the account in THIS service (the source of
        // truth) already exists by the time this runs. A ProjectAPI outage
        // must not roll back or fail an otherwise-successful admin-create —
        // the mirror row can be backfilled later. Logged loudly so the gap
        // is visible, not silent.
        try
        {
            var response = await _httpClient.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "[ProjectApiClient] Failed to provision user {UserId} in ProjectAPI: {Status} {Body}",
                    request.Id, response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ProjectApiClient] Could not reach ProjectAPI to provision user {UserId}.", request.Id);
        }
    }
}
