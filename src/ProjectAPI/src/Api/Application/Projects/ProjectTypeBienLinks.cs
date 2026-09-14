using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Projects;

/// <summary>
/// The property types a project offers (Project ↔ TypeBien), set from the
/// project form as a whole list. Linking used to exist only as a one-by-one
/// endpoint the back office never called, with no way to unlink.
/// </summary>
internal static class ProjectTypeBienLinks
{
    /// <summary>Makes the project's links exactly <paramref name="typeBienIds"/>. Unknown ids → 422.</summary>
    public static async Task SyncAsync(ApplicationDbContext db, Guid projectId, IReadOnlyCollection<int> typeBienIds, CancellationToken ct)
    {
        var wanted = typeBienIds.Distinct().ToList();
        var known = await db.Set<TypeBien>().Where(t => wanted.Contains(t.Id)).Select(t => t.Id).ToListAsync(ct);
        var unknown = wanted.Except(known).ToList();
        if (unknown.Count > 0)
        {
            throw new Common.Exceptions.ValidationException(new[]
            {
                new ValidationFailure("TypeBienIds", $"Type(s) de bien inconnu(s) : {string.Join(", ", unknown)}.")
            });
        }

        var current = await db.Set<ProjectTypeBien>().Where(l => l.ProjectId == projectId).ToListAsync(ct);
        db.RemoveRange(current.Where(l => l.TypeBienId is null || !wanted.Contains(l.TypeBienId.Value)));
        foreach (var id in wanted.Where(id => current.All(l => l.TypeBienId != id)))
        {
            db.Add(new ProjectTypeBien { Id = Guid.NewGuid(), ProjectId = projectId, TypeBienId = id });
        }
        await db.SaveChangesAsync(ct);
    }

    public static async Task EnsureQuartierExistsAsync(ApplicationDbContext db, Guid? quartierId, CancellationToken ct)
    {
        if (quartierId is null) return;
        if (!await db.Set<Quartier>().AnyAsync(q => q.Id == quartierId, ct))
        {
            throw new Common.Exceptions.ValidationException(new[]
            {
                new ValidationFailure("QuartierId", "Quartier inconnu.")
            });
        }
    }
}
