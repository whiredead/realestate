namespace ProjectAPI.Api.Application.Common.Idempotency;

/// <summary>
/// §7 — "Idempotency-Key required on: reservation submit/approve, payment
/// creation, import validate/commit, campaign send, final/notary appointment
/// creation, handover completion." A command implements this to opt into
/// <see cref="IdempotencyBehaviour{TRequest,TResponse}"/>: the same key with
/// the same body replays the stored response instead of re-running the
/// handler; the same key with a different body is rejected with
/// 409 IDEMPOTENCY_KEY_REUSED.
///
/// Commands that do NOT implement this run through the pipeline unaffected —
/// idempotency is opt-in per endpoint, not a blanket behavior, since most
/// commands (queries, read-modify writes gated by their own state machine)
/// have no need for it.
/// </summary>
public interface IIdempotentRequest
{
    /// <summary>
    /// Caller-supplied key (typically the Idempotency-Key HTTP header, bound by
    /// the controller). Null/empty means the caller opted out for this call —
    /// the behavior passes it straight through with no dedup.
    /// </summary>
    string? IdempotencyKey { get; set; }
}
