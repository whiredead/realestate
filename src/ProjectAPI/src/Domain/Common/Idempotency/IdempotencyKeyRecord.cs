namespace ProjectAPI.Domain.Common.Idempotency;

/// <summary>
/// §7 — one row per (endpoint, caller-supplied key) pair. A first call with a
/// key stores its request-body hash and, once the handler completes, its
/// response; a replay with the same key returns the stored response without
/// re-running the handler. The same key with a DIFFERENT body is a client
/// bug (retried the wrong call, or two requests collided on one key) and must
/// fail loudly rather than silently do whichever body arrived first —
/// hence <see cref="RequestHash"/>.
/// </summary>
public class IdempotencyKeyRecord
{
    public Guid Id { get; set; }

    /// <summary>Caller-supplied key, unique per <see cref="Operation"/>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The MediatR request type name (e.g. "CreateReservationCommand"). Scopes
    /// the key so the same string used against two different endpoints does
    /// not collide.
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>SHA-256 of the serialized request body, to detect key reuse with a different payload.</summary>
    public string RequestHash { get; set; } = string.Empty;

    /// <summary>Serialized response, populated once the handler completes successfully.</summary>
    public string? ResponseBody { get; set; }

    /// <summary>CLR type name of the response, needed to deserialize it back on replay.</summary>
    public string? ResponseTypeName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }
}
