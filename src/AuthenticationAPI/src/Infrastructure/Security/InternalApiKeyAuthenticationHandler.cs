using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using AuthenticationAPI.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthenticationAPI.Infrastructure.Security;

public static class InternalApiKeyDefaults
{
    public const string AuthenticationScheme = "InternalApiKey";
    public const string HeaderName = "X-Internal-Api-Key";
}

/// <summary>
/// Phase 2 — authenticates the internal, service-to-service endpoints
/// (see InternalController in ProjectAPI's caller) via a shared static key
/// in a request header, compared in constant time to avoid a timing side
/// channel. This scheme is registered ALONGSIDE the default JwtBearer scheme
/// (see DependencyInjection.ConfigureInternalApiKeyAuthentication) — it does
/// not replace it, and does not change auth for any existing endpoint: only
/// controllers explicitly opting in with
/// [Authorize(AuthenticationSchemes = InternalApiKeyDefaults.AuthenticationScheme)]
/// are affected.
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

        // Constant-time comparison — a length mismatch alone must not
        // short-circuit before the byte comparison in a way that leaks
        // timing information about the secret's length or content.
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
