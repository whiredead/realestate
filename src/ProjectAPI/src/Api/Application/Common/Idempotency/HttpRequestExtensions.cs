namespace ProjectAPI.Api.Application.Common.Idempotency;

/// <summary>
/// §7 — reads the caller-supplied <c>Idempotency-Key</c> header. A thin helper
/// so every controller action that binds it does so the same way, rather than
/// repeating <c>Request.Headers["Idempotency-Key"]</c> ad hoc.
/// </summary>
public static class HttpRequestExtensions
{
    public const string HeaderName = "Idempotency-Key";

    public static string? GetIdempotencyKey(this HttpRequest request) =>
        request.Headers.TryGetValue(HeaderName, out var values) && values.Count > 0
            ? values[0]
            : null;
}
