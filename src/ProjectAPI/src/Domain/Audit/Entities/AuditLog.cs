namespace ProjectAPI.Domain.Audit.Entities;

/// <summary>
/// One business action performed by a user (spec §7, §48.10 <c>audit_logs</c>).
/// Append-only: written in the same transaction as the change it describes,
/// never updated by any endpoint and never deleted.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }
    public DateTime OccurredAt { get; set; }

    /// <summary><see cref="AuditCategories"/>: business change or security event.</summary>
    public string Category { get; set; } = AuditCategories.Business;

    /// <summary>Stable UPPER_SNAKE code, e.g. APPROVE_RESERVATION. Never renamed: clients translate it.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary><see cref="AuditOutcomes"/>. Business rows are always SUCCESS; security rows record failures too.</summary>
    public string Outcome { get; set; } = AuditOutcomes.Success;

    /// <summary>AspNetUsers.Id of the caller; null for anonymous calls (login, password reset).</summary>
    public string? ActorUserId { get; set; }

    /// <summary>Snapshot of who acted when no account id is known (the e-mail typed at login).</summary>
    public string? ActorLabel { get; set; }

    /// <summary>Caller roles at the time of the action, comma separated.</summary>
    public string? ActorRoles { get; set; }

    /// <summary>Main record acted on (entity CLR name + key). Every record touched is listed in <see cref="Subjects"/>.</summary>
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }

    /// <summary>Project the action belongs to; drives the PROJECT_ADMIN perimeter. Null for global data.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>JSON array of per-record changes: [{ entity, id, op, fields: { name: { from, to } } }].</summary>
    public string? Changes { get; set; }

    /// <summary>Failure reason for a refused security event.</summary>
    public string? Detail { get; set; }

    public string? RequestId { get; set; }
    public string? IpAddress { get; set; }

    public List<AuditLogSubject> Subjects { get; set; } = new();
}

/// <summary>Index row: one per record an <see cref="AuditLog"/> touched, so a record's history is one indexed lookup.</summary>
public class AuditLogSubject
{
    public Guid Id { get; set; }
    public Guid AuditLogId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
}

public static class AuditCategories
{
    public const string Business = "BUSINESS";
    public const string Security = "SECURITY";
    /// <summary>A staff member opened or downloaded a buyer's personal data. Visible to GLOBAL_ADMIN only.</summary>
    public const string Access = "ACCESS";
}

public static class AuditOutcomes
{
    public const string Success = "SUCCESS";
    public const string Failure = "FAILURE";
}
