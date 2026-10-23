using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ProjectAPI.Domain.Audit.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Infrastructure.Auditing;

/// <summary>
/// §7 — writes the audit row inside the same SaveChanges as the business change, so
/// both commit or neither does. Runs only while an audited command is executing
/// (see <see cref="IAuditContext"/>); saves outside a command are not attributed.
///
/// A command that saves several times still produces ONE row: later saves append
/// their changes and subjects to the row written by the first.
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IAuditContext _audit;
    private AuditLog? _addedThisSave;

    public AuditSaveChangesInterceptor(IAuditContext audit) => _audit = audit;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is ApplicationDbContext db) await CaptureAsync(db, cancellationToken);
        return result;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is ApplicationDbContext db) CaptureAsync(db, CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Rollback(eventData.Context);
        return Task.CompletedTask;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Rollback(eventData.Context);

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        _addedThisSave = null;
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        _addedThisSave = null;
        return result;
    }

    private async Task CaptureAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var invocation = _audit.Current;
        if (invocation is null || invocation.Category != AuditCategories.Business) return;

        db.ChangeTracker.DetectChanges();
        var entries = AuditChangeCollector.PendingEntries(db);
        if (entries.Count == 0) return;

        foreach (var e in entries)
            if (invocation.Changes.Count < AuditChangeCollector.MaxChangesPerRow)
                invocation.Changes.Add(AuditChangeCollector.Describe(e));

        var subjects = entries.SelectMany(AuditChangeCollector.SubjectsOf)
            .Concat(AuditChangeCollector.SubjectsOf(invocation.Request))
            .ToList();

        var row = invocation.Row;
        if (row is null)
        {
            row = new AuditLog
            {
                Id = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow,
                Category = invocation.Category,
                Action = invocation.Action,
                Outcome = AuditOutcomes.Success,
                ActorUserId = invocation.ActorUserId,
                ActorLabel = invocation.ActorLabel,
                ActorRoles = invocation.ActorRoles,
                RequestId = invocation.RequestId,
                IpAddress = invocation.IpAddress
            };
            (row.EntityType, row.EntityId) = MainRecord(invocation, entries);
            row.ProjectId = await ResolveProjectAsync(db, subjects, ct);
            invocation.Row = row;
            _addedThisSave = row;
            db.Add(row);
        }
        else
        {
            row.ProjectId ??= await ResolveProjectAsync(db, subjects, ct);
        }

        row.Changes = JsonSerializer.Serialize(invocation.Changes, Json);
        var total = entries.Count;
        if (total > AuditChangeCollector.MaxChangesPerRow)
            row.Detail = $"{total} enregistrements modifiés ; seuls les {AuditChangeCollector.MaxChangesPerRow} premiers sont détaillés.";

        foreach (var (type, id) in subjects)
        {
            if (id.Length > 64 || !invocation.SubjectKeys.Add($"{type}:{id}")) continue;
            db.Add(new AuditLogSubject { Id = Guid.NewGuid(), AuditLogId = row.Id, EntityType = type, EntityId = id });
        }
    }

    /// <summary>The request's own Id when a changed record carries it, else the first record changed.</summary>
    private static (string?, string?) MainRecord(AuditInvocation invocation, List<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry> entries)
    {
        var requestId = AuditChangeCollector.RequestId(invocation.Request);
        // CREATE_APPOINTMENT is about the Appointment, not the CRM contact it also touched.
        var action = invocation.Action.Replace("_", "");
        var named = entries
            .Where(e => action.Contains(AuditChangeCollector.TypeName(e.Metadata.ClrType), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => AuditChangeCollector.TypeName(e.Metadata.ClrType).Length)
            .FirstOrDefault();
        var main = (requestId is null ? null : entries.FirstOrDefault(e => AuditChangeCollector.KeyOf(e) == requestId))
            ?? named
            ?? entries.FirstOrDefault(e => e.State == EntityState.Modified)
            ?? entries[0];
        return (AuditChangeCollector.TypeName(main.Metadata.ClrType), AuditChangeCollector.KeyOf(main));
    }

    private static async Task<Guid?> ResolveProjectAsync(ApplicationDbContext db, List<(string Type, string Id)> subjects, CancellationToken ct)
    {
        var refs = new ProjectRefs();
        foreach (var (type, id) in subjects) AuditChangeCollector.AddProjectRef(refs, type, id);
        if (refs.IsEmpty) return null;
        var projects = await ProjectChain.ResolveAsync(db, refs, ct);
        return projects.Count > 0 ? projects.First() : null;
    }

    /// <summary>A failed save must not leave a half-written row tracked for the next attempt.</summary>
    private void Rollback(DbContext? db)
    {
        var invocation = _audit.Current;
        if (db is null || invocation is null || _addedThisSave is null) return;
        foreach (var subject in db.ChangeTracker.Entries<AuditLogSubject>().Where(s => s.Entity.AuditLogId == _addedThisSave.Id).ToList())
            subject.State = EntityState.Detached;
        db.Entry(_addedThisSave).State = EntityState.Detached;
        invocation.Row = null;
        invocation.Changes.Clear();
        invocation.SubjectKeys.Clear();
        _addedThisSave = null;
    }
}
