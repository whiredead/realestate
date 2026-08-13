namespace ProjectAPI.Infrastructure.Settings;

/// <summary>
/// Shared secret protecting internal, service-to-service endpoints (see
/// InternalApiKeyAuthenticationHandler) — same posture and same value as
/// AuthenticationAPI's InternalApiSettings (they authenticate the two
/// directions of the same service-to-service relationship). A real
/// deployment must override this via environment variable/user-secrets, not
/// this file, and should rotate it independently of the JWT signing key.
/// </summary>
public class InternalApiSettings
{
    public string ApiKey { get; set; } = default!;

    /// <summary>Base URL for AuthenticationAPI's own internal/ endpoints — used by the Phase 2 invitation-acceptance flow to provision the BUYER account.</summary>
    public string AuthApiBaseUrl { get; set; } = default!;
}
