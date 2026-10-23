namespace ProjectAPI.Api.Application.Common.Audit;

/// <summary>
/// Implemented by a response that can report failure without an exception
/// (e.g. a refused login answered 200 with IsAutheticated = false), so the
/// audit trail records the real outcome of a security event.
/// </summary>
public interface IAuditOutcome
{
    bool Succeeded { get; }
    string? FailureReason { get; }
    /// <summary>The account the action resolved to (a login knows who it was only once it succeeds).</summary>
    string? ActorUserId { get; }
    /// <summary>That account's roles, comma separated.</summary>
    string? ActorRoles { get; }
}
