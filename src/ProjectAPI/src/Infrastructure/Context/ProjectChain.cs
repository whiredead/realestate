using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Sales.Entities;

namespace ProjectAPI.Infrastructure.Context;

/// <summary>
/// Ids collected from changed rows at each level of the ownership chain
/// final-visit report → appointment → case → reservation → unit → immeuble → project.
/// </summary>
internal sealed class ProjectRefs
{
    public HashSet<Guid> Projects { get; } = new();
    public HashSet<Guid> Immeubles { get; } = new();
    public HashSet<Guid> Units { get; } = new();
    public HashSet<Guid> Reservations { get; } = new();
    public HashSet<Guid> VisitCases { get; } = new();
    public HashSet<Guid> VisitAppointments { get; } = new();
    /// <summary>After-sales claims (audit only: the read-only guard deliberately leaves SAV open).</summary>
    public HashSet<Guid> Claims { get; } = new();
    /// <summary>Commercial visits and what hangs off them (visit reports): audit only.</summary>
    public HashSet<Guid> Appointments { get; } = new();

    public bool IsEmpty => Projects.Count + Immeubles.Count + Units.Count + Reservations.Count + VisitCases.Count + VisitAppointments.Count + Claims.Count + Appointments.Count == 0;
}

/// <summary>Walks <see cref="ProjectRefs"/> up to the owning project ids, one query per level.</summary>
internal static class ProjectChain
{
    public static async Task<HashSet<Guid>> ResolveAsync(ApplicationDbContext db, ProjectRefs refs, CancellationToken ct)
    {
        var projectIds = new HashSet<Guid>(refs.Projects);
        var immeubleIds = new HashSet<Guid>(refs.Immeubles);
        var unitIds = new HashSet<Guid>(refs.Units);
        var reservationIds = new HashSet<Guid>(refs.Reservations);
        var visitCaseIds = new HashSet<Guid>(refs.VisitCases);

        if (refs.Appointments.Count > 0)
        {
            var ids = refs.Appointments.ToList();
            foreach (var pid in await db.Set<Appointment>().AsNoTracking().Where(a => ids.Contains(a.Id)).Select(a => a.ProjectId).ToListAsync(ct))
                projectIds.Add(pid);
        }

        if (refs.VisitAppointments.Count > 0)
        {
            var ids = refs.VisitAppointments.ToList();
            foreach (var caseId in await db.Set<FinalVisitAppointment>().AsNoTracking().Where(a => ids.Contains(a.Id)).Select(a => a.CaseId).ToListAsync(ct))
                visitCaseIds.Add(caseId);
        }
        if (visitCaseIds.Count > 0)
        {
            var ids = visitCaseIds.ToList();
            foreach (var resId in await db.Set<FinalVisitCase>().AsNoTracking().Where(c => ids.Contains(c.Id)).Select(c => c.ReservationId).ToListAsync(ct))
                reservationIds.Add(resId);
        }
        if (reservationIds.Count > 0)
        {
            var ids = reservationIds.ToList();
            foreach (var unitId in await db.Set<Reservation>().AsNoTracking().Where(r => ids.Contains(r.Id)).Select(r => r.UnitId).ToListAsync(ct))
                unitIds.Add(unitId);
        }
        if (refs.Claims.Count > 0)
        {
            var ids = refs.Claims.ToList();
            foreach (var unitId in await db.Set<AfterSaleClaim>().AsNoTracking().Where(c => ids.Contains(c.Id)).Select(c => c.UnitId).ToListAsync(ct))
                unitIds.Add(unitId);
        }
        if (unitIds.Count > 0)
        {
            var ids = unitIds.ToList();
            // Unit.ProjectId is the FK to its Immeuble (see Unit.cs).
            foreach (var immeubleId in await db.Set<Unit>().AsNoTracking().Where(u => ids.Contains(u.Id)).Select(u => u.ProjectId).ToListAsync(ct))
                immeubleIds.Add(immeubleId);
        }
        if (immeubleIds.Count > 0)
        {
            var ids = immeubleIds.ToList();
            foreach (var projectId in await db.Set<Immeuble>().AsNoTracking().Where(i => ids.Contains(i.Id)).Select(i => i.ProjectId).ToListAsync(ct))
                projectIds.Add(projectId);
        }
        return projectIds;
    }
}
