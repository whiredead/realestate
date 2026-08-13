namespace AuthenticationAPI.Infrastructure.Settings;

/// <summary>
/// Phase 2 — shared secret protecting the internal, service-to-service
/// endpoints (see InternalApiKeyAuthenticationHandler). A static key is
/// proportionate here: two services on the same private network, no
/// external exposure of this route, and no existing OAuth/client-credentials
/// infrastructure in this solution to build on instead. The dev value below
/// is a placeholder, same posture as JwtSettings.SecretKey already committed
/// in appsettings.json — a real deployment must override it via environment
/// variable/user-secrets, not this file, and should rotate it independently
/// of the JWT signing key.
/// </summary>
public class InternalApiSettings
{
    public string ApiKey { get; set; } = default!;

    /// <summary>Base URL for ProjectAPI's own internal endpoints (see IProjectApiClient) — the reverse direction of this same service-to-service relationship.</summary>
    public string ProjectApiBaseUrl { get; set; } = default!;
}
