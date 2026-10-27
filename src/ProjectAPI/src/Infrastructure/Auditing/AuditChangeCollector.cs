using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ProjectAPI.Domain.Audit.Entities;
using ProjectAPI.Domain.Common.Idempotency;
using ProjectAPI.Domain.Identity.Entities;
using ProjectAPI.Domain.Notifications.Entities;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Infrastructure.Auditing;

/// <summary>One changed record inside an audit row (serialised into <see cref="AuditLog.Changes"/>).</summary>
public sealed record AuditChange(string Entity, string Id, string Op, Dictionary<string, AuditFieldChange>? Fields);
public sealed record AuditFieldChange(object? From, object? To);

/// <summary>Turns the change tracker's pending rows into audit changes, subjects and owning projects.</summary>
public static class AuditChangeCollector
{
    public const int MaxChangesPerRow = 50;
    private const int MaxStringLength = 300;

    /// <summary>Technical rows that say nothing about a business action.</summary>
    private static readonly HashSet<Type> IgnoredTypes =
    [
        typeof(AuditLog), typeof(AuditLogSubject), typeof(IdempotencyKeyRecord),
        typeof(Notification), typeof(SentReminder), typeof(SessionRefreshToken)
    ];

    /// <summary>Never copied into the log, whatever the entity.</summary>
    private static readonly HashSet<string> SecretProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "Password", "SecurityStamp", "ConcurrencyStamp", "TokenHash", "Token", "RefreshToken"
    };

    /// <summary>Who-did-it columns: the actor is already on the row, so these are not subjects.</summary>
    private static readonly HashSet<string> ActorProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "ActorUserId", "AuthorUserId", "CreatedBy", "ConnectedUserId", "UpdatedByUserId", "ChangedByUserId", "UpdatedBy"
    };

    /// <summary>Request properties that name a record ("ReservationId" → Reservation), for subjects and project resolution.</summary>
    private static readonly Dictionary<string, string> RequestIdProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ProjectId"] = "Project", ["ImmeubleId"] = "Immeuble", ["UnitId"] = "Unit", ["ReservationId"] = "Reservation",
        ["ClaimId"] = nameof(AfterSaleClaim), ["UserId"] = "User", ["AgentId"] = "User", ["NotaryId"] = "User", ["TargetUserId"] = "User"
    };

    public static List<EntityEntry> PendingEntries(DbContext db) =>
        db.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => !IgnoredTypes.Contains(e.Metadata.ClrType))
            .ToList();

    /// <summary>Identity subclasses (Agent, Notary) and the role join are shown as what they are to a reader.</summary>
    public static string TypeName(Type clr)
    {
        if (typeof(ProjectAPI.Domain.Users.Entities.User).IsAssignableFrom(clr)) return "User";
        return clr.Name;
    }

    public static string KeyOf(EntityEntry e) =>
        string.Join("|", e.Metadata.FindPrimaryKey()!.Properties.Select(p => Convert.ToString(e.Property(p.Name).CurrentValue)));

    public static AuditChange Describe(EntityEntry e)
    {
        var op = e.State.ToString();
        Dictionary<string, AuditFieldChange>? fields = null;
        if (e.State is EntityState.Modified or EntityState.Added)
        {
            fields = new Dictionary<string, AuditFieldChange>();
            foreach (var p in e.Properties)
            {
                var name = p.Metadata.Name;
                if (SecretProperties.Contains(name) || p.Metadata.IsPrimaryKey() || p.Metadata.ClrType == typeof(byte[])) continue;
                if (e.State == EntityState.Modified)
                {
                    if (!p.IsModified || SameValue(p.OriginalValue, p.CurrentValue)) continue;
                    fields[name] = new AuditFieldChange(Format(p.OriginalValue), Format(p.CurrentValue));
                }
                else if (p.CurrentValue is not null && !p.Metadata.IsShadowProperty())
                {
                    fields[name] = new AuditFieldChange(null, Format(p.CurrentValue));
                }
            }
        }
        return new AuditChange(TypeName(e.Metadata.ClrType), KeyOf(e), op, fields);
    }

    /// <summary>The record itself plus every record it points to through a foreign key.</summary>
    public static IEnumerable<(string Type, string Id)> SubjectsOf(EntityEntry e)
    {
        yield return (TypeName(e.Metadata.ClrType), KeyOf(e));
        foreach (var p in e.Properties)
        {
            if (!p.Metadata.IsForeignKey() || ActorProperties.Contains(p.Metadata.Name)) continue;
            var value = e.State == EntityState.Deleted ? p.OriginalValue : p.CurrentValue;
            if (value is null) continue;
            var principal = p.Metadata.GetContainingForeignKeys().First().PrincipalEntityType.ClrType;
            if (IgnoredTypes.Contains(principal)) continue;
            yield return (TypeName(principal), Convert.ToString(value)!);
        }
    }

    public static IEnumerable<(string Type, string Id)> SubjectsOf(object request)
    {
        foreach (var prop in request.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!RequestIdProperties.TryGetValue(prop.Name, out var type)) continue;
            var value = prop.GetValue(request);
            if (value is null || (value is Guid g && g == Guid.Empty) || (value is string s && string.IsNullOrWhiteSpace(s))) continue;
            yield return (type, Convert.ToString(value)!);
        }
    }

    /// <summary>The request's own <c>Id</c>, used to pick the main record among the changed ones.</summary>
    public static string? RequestId(object request)
    {
        var value = request.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)?.GetValue(request);
        return value is null ? null : Convert.ToString(value);
    }

    internal static void AddProjectRef(ProjectRefs refs, string type, string id)
    {
        if (!Guid.TryParse(id, out var g)) return;
        switch (type)
        {
            case "Project": refs.Projects.Add(g); break;
            case "Immeuble": refs.Immeubles.Add(g); break;
            case "Unit": refs.Units.Add(g); break;
            case "Reservation": refs.Reservations.Add(g); break;
            case "FinalVisitCase": refs.VisitCases.Add(g); break;
            case "FinalVisitAppointment": refs.VisitAppointments.Add(g); break;
            case nameof(AfterSaleClaim): refs.Claims.Add(g); break;
            case "Appointment": refs.Appointments.Add(g); break;
        }
    }

    /// <summary>Equal values, including a list re-saved with the same items (EF marks a new list instance as modified).</summary>
    private static bool SameValue(object? a, object? b) =>
        Equals(a, b)
        || (a is System.Collections.IEnumerable x && b is System.Collections.IEnumerable y && a is not string && b is not string
            && x.Cast<object?>().SequenceEqual(y.Cast<object?>()));

    private static object? Format(object? value) => value switch
    {
        null => null,
        Enum en => en.ToString(),
        string s when s.Length > MaxStringLength => s[..MaxStringLength] + "…",
        DateTime or DateTimeOffset or Guid or bool or string => value,
        IConvertible c when value.GetType().IsPrimitive || value is decimal => c,
        // A collection column (e.g. the unit ids a visit report lists): keep the items, not "System.Collections.Generic.List`1[...]".
        System.Collections.IEnumerable items => items.Cast<object?>().Select(Format).ToList(),
        _ => Convert.ToString(value)
    };
}
