using ProjectAPI.Domain.Audit.Entities;

namespace ProjectAPI.Infrastructure.Auditing;

/// <summary>
/// The command being executed in this request, as seen by the audit trail.
/// Set by the MediatR <c>AuditBehaviour</c>; read by <see cref="AuditSaveChangesInterceptor"/>
/// so the rows changed by a save are attributed to the business action that caused them.
/// </summary>
public sealed class AuditInvocation
{
    public required string Action { get; init; }
    public required string Category { get; init; }
    public required object Request { get; init; }
    public string? ActorUserId { get; init; }
    public string? ActorLabel { get; init; }
    public string? ActorRoles { get; init; }
    public string? RequestId { get; init; }
    public string? IpAddress { get; init; }

    // Accumulated across every SaveChanges of the command: one command = one audit row.
    internal AuditLog? Row { get; set; }
    internal List<AuditChange> Changes { get; } = new();
    internal HashSet<string> SubjectKeys { get; } = new(StringComparer.Ordinal);
}

/// <summary>Actor label of actions no person performed (scheduled jobs).</summary>
public static class AuditActors
{
    public const string System = "Système";
}

public interface IAuditContext
{
    AuditInvocation? Current { get; }
    void Begin(AuditInvocation invocation);
    void End();
}

/// <summary>Scoped: one per HTTP request, shared by the behaviour and the DbContext's interceptor.</summary>
public sealed class AuditContext : IAuditContext
{
    public AuditInvocation? Current { get; private set; }
    public void Begin(AuditInvocation invocation) => Current = invocation;
    public void End() => Current = null;
}
