using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Domain.Handovers.Entities;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Sales.Entities;

namespace ProjectAPI.Infrastructure.Context;

/// <summary>
/// Enforces <see cref="ProjectStatusCodes.IsReadOnly"/> for every write, in one
/// place: a FINALISE (or SUSPENDED) project accepts no business mutation — its
/// data, buildings, floors, units, construction, reservations and their
/// documents, final visits, sales, notary appointments and handovers.
///
/// The rule existed as a helper nobody called, so a finalised project could
/// still be edited, re-staffed with buildings or take new reservations through
/// any handler that only checked its own narrower precondition.
///
/// Deliberately NOT covered: after-sales claims, warranties, notifications,
/// memberships and audit rows — the warranty outlives the project's
/// commercial life, and staffing/consultation continue.
///
/// The status checked is the one stored in the database, so the command that
/// finalises a project (which changes its status in this same save) passes.
/// </summary>
internal static class ProjectReadOnlyGuard
{
    public static async Task EnsureAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var entries = db.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        if (entries.Count == 0) return;

        var projectIds = new HashSet<Guid>();
        var immeubleIds = new HashSet<Guid>();
        var unitIds = new HashSet<Guid>();
        var reservationIds = new HashSet<Guid>();
        var visitCaseIds = new HashSet<Guid>();
        var visitAppointmentIds = new HashSet<Guid>();

        foreach (var e in entries)
        {
            switch (e.Entity)
            {
                case Project p when e.State != EntityState.Added:
                    projectIds.Add(p.Id);
                    break;
                case Immeuble im:
                    projectIds.Add(Original(e, nameof(Immeuble.ProjectId), im.ProjectId));
                    break;
                case Floor f:
                    immeubleIds.Add(Original(e, nameof(Floor.ImmeubleId), f.ImmeubleId));
                    break;
                case Unit u:
                    // Unit.ProjectId is the FK to its Immeuble (see Unit.cs).
                    immeubleIds.Add(Original(e, nameof(Unit.ProjectId), u.ProjectId));
                    break;
                case ConstructionMilestone m:
                    projectIds.Add(m.ProjectId);
                    break;
                case ConstructionUpdate cu:
                    projectIds.Add(cu.ProjectId);
                    break;
                case ProjectDocumentRequirement r:
                    projectIds.Add(r.ProjectId);
                    break;
                case ProjectFeature pf:
                    projectIds.Add(pf.ProjectId);
                    break;
                case ProjectTypeBien ptb when ptb.ProjectId.HasValue:
                    projectIds.Add(ptb.ProjectId.Value);
                    break;
                case Reservation r:
                    unitIds.Add(r.UnitId);
                    break;
                case ReservationDocument d:
                    reservationIds.Add(d.ReservationId);
                    break;
                case Sale s:
                    unitIds.Add(s.UnitId);
                    break;
                case NotaryAppointment na:
                    reservationIds.Add(na.ReservationId);
                    break;
                case HandoverAppointment ha:
                    unitIds.Add(ha.UnitId);
                    break;
                case FinalVisitCase fc:
                    reservationIds.Add(fc.ReservationId);
                    break;
                case FinalVisitAppointment fa:
                    visitCaseIds.Add(fa.CaseId);
                    break;
                case FinalVisitReport fr:
                    visitAppointmentIds.Add(fr.AppointmentId);
                    break;
            }
        }

        if (projectIds.Count + immeubleIds.Count + unitIds.Count + reservationIds.Count + visitCaseIds.Count + visitAppointmentIds.Count == 0)
            return;

        if (visitAppointmentIds.Count > 0)
        {
            var ids = visitAppointmentIds.ToList();
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
        if (unitIds.Count > 0)
        {
            var ids = unitIds.ToList();
            foreach (var immeubleId in await db.Set<Unit>().AsNoTracking().Where(u => ids.Contains(u.Id)).Select(u => u.ProjectId).ToListAsync(ct))
                immeubleIds.Add(immeubleId);
        }
        if (immeubleIds.Count > 0)
        {
            var ids = immeubleIds.ToList();
            foreach (var projectId in await db.Set<Immeuble>().AsNoTracking().Where(i => ids.Contains(i.Id)).Select(i => i.ProjectId).ToListAsync(ct))
                projectIds.Add(projectId);
        }

        if (projectIds.Count == 0) return;
        var projectIdList = projectIds.ToList();
        var stored = await db.Set<Project>().AsNoTracking()
            .Where(p => projectIdList.Contains(p.Id))
            .Select(p => new { p.Id, p.StatusGlobal })
            .ToListAsync(ct);

        var readOnly = stored.FirstOrDefault(p => ProjectStatusCodes.IsReadOnly(p.StatusGlobal));
        if (readOnly is not null) throw new ProjectReadOnlyException(readOnly.Id);
    }

    /// <summary>For a moved/deleted row the stored (original) parent is what counts.</summary>
    private static Guid Original(EntityEntry e, string property, Guid current) =>
        e.State == EntityState.Added ? current : (Guid)e.Property(property).OriginalValue!;
}
