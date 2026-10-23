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

        var refs = new ProjectRefs();

        foreach (var e in entries)
        {
            switch (e.Entity)
            {
                case Project p when e.State != EntityState.Added:
                    refs.Projects.Add(p.Id);
                    break;
                case Immeuble im:
                    refs.Projects.Add(Original(e, nameof(Immeuble.ProjectId), im.ProjectId));
                    break;
                case Floor f:
                    refs.Immeubles.Add(Original(e, nameof(Floor.ImmeubleId), f.ImmeubleId));
                    break;
                case Unit u:
                    // Unit.ProjectId is the FK to its Immeuble (see Unit.cs).
                    refs.Immeubles.Add(Original(e, nameof(Unit.ProjectId), u.ProjectId));
                    break;
                case ConstructionMilestone m:
                    refs.Projects.Add(m.ProjectId);
                    break;
                case ConstructionUpdate cu:
                    refs.Projects.Add(cu.ProjectId);
                    break;
                case ProjectDocumentRequirement r:
                    refs.Projects.Add(r.ProjectId);
                    break;
                case ProjectFeature pf:
                    refs.Projects.Add(pf.ProjectId);
                    break;
                case ProjectTypeBien ptb when ptb.ProjectId.HasValue:
                    refs.Projects.Add(ptb.ProjectId.Value);
                    break;
                case Reservation r:
                    refs.Units.Add(r.UnitId);
                    break;
                case ReservationDocument d:
                    refs.Reservations.Add(d.ReservationId);
                    break;
                case Sale s:
                    refs.Units.Add(s.UnitId);
                    break;
                case NotaryAppointment na:
                    refs.Reservations.Add(na.ReservationId);
                    break;
                case HandoverAppointment ha:
                    refs.Units.Add(ha.UnitId);
                    break;
                case FinalVisitCase fc:
                    refs.Reservations.Add(fc.ReservationId);
                    break;
                case FinalVisitAppointment fa:
                    refs.VisitCases.Add(fa.CaseId);
                    break;
                case FinalVisitReport fr:
                    refs.VisitAppointments.Add(fr.AppointmentId);
                    break;
            }
        }

        if (refs.IsEmpty) return;

        var projectIds = await ProjectChain.ResolveAsync(db, refs, ct);
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
