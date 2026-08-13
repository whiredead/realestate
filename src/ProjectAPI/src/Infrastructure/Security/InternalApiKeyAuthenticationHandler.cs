using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectAPI.Infrastructure.Settings;

namespace ProjectAPI.Infrastructure.Security;

public static class InternalApiKeyDefaults
{
    public const string AuthenticationScheme = "InternalApiKey";
    public const string HeaderName = "X-Internal-Api-Key";
}

/// <summary>
/// Authenticates internal, service-to-service endpoints (see
/// InternalController) via a shared static key in a request header — the
/// mirror direction of AuthenticationAPI's own InternalApiKeyAuthenticationHandler.
/// AuthenticationAPI's admin-create-user endpoint calls THIS service's
/// internal endpoint so a newly created SALES_AGENT/NOTARY account gets a
/// matching AspNetUsers row here too — the two services keep separate
/// AspNetUsers tables, and nothing else keeps them in sync.
/// </summary>
public class InternalApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly InternalApiSettings _settings;

    public InternalApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        InternalApiSettings settings)
        : base(options, logger, encoder)
    {
        _settings = settings;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(InternalApiKeyDefaults.HeaderName, out var provided) ||
            string.IsNullOrEmpty(provided))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing internal API key."));
        }

        var expectedBytes = Encoding.UTF8.GetBytes(_settings.ApiKey);
        var providedBytes = Encoding.UTF8.GetBytes(provided.ToString());

        var isValid = expectedBytes.Length == providedBytes.Length &&
                       System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);

        if (!isValid)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid internal API key."));
        }

        var identity = new ClaimsIdentity(InternalApiKeyDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, InternalApiKeyDefaults.AuthenticationScheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
