using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Audit.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Infrastructure.Auditing;

/// <summary>
/// Writes a consultation row (category ACCESS): who opened which buyer record. The
/// record is loaded to find what it belongs to (a document → its reservation → unit →
/// project), so the row appears in the reservation's history and carries its project.
/// </summary>
public static class AuditAccessRecorder
{
    public static async Task RecordAsync(ApplicationDbContext db, AuditLog row, string entityType, Guid id, CancellationToken ct)
    {
        row.EntityType = entityType;
        row.EntityId = id.ToString();
        var subjects = new List<(string Type, string Id)> { (entityType, row.EntityId) };

        var clr = db.Model.GetEntityTypes().FirstOrDefault(t => t.ClrType.Name == entityType)?.ClrType;
        var entity = clr is null ? null : await db.FindAsync(clr, new object[] { id }, ct);
        if (entity is not null)
            subjects.AddRange(AuditChangeCollector.SubjectsOf(db.Entry(entity)).Skip(1));

        var refs = new ProjectRefs();
        foreach (var (type, sid) in subjects) AuditChangeCollector.AddProjectRef(refs, type, sid);
        if (!refs.IsEmpty) row.ProjectId = (await ProjectChain.ResolveAsync(db, refs, ct)).Cast<Guid?>().FirstOrDefault();

        db.Add(row);
        foreach (var (type, sid) in subjects.Distinct())
            db.Add(new AuditLogSubject { Id = Guid.NewGuid(), AuditLogId = row.Id, EntityType = type, EntityId = sid });
        await db.SaveChangesAsync(ct);
    }
}
