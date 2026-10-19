using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Infrastructure.Context;
using UnitEntity = ProjectAPI.Domain.Immeubles.Entities.Unit;

namespace ProjectAPI.Api.Application.FinalVisits.GetFinalVisitCases;

/// <summary>
/// The final-visit worklist: every case in the caller's projects with its
/// latest attempt, current report and active snags. Detail and actions stay
/// on GET reservations/{id}/case.
/// </summary>
public class GetFinalVisitCasesQuery : IRequest<PaginatedResponse<FinalVisitCaseListItem>>
{
    public Guid? ProjectId { get; set; }

    /// <summary>Open, RevisitRequired, ReadyForNotary or Closed.</summary>
    public string? Status { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class FinalVisitCaseListItem
{
    public Guid CaseId { get; set; }
    public Guid ReservationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; }

    public string? BuyerName { get; set; }
    public Guid UnitId { get; set; }
    public string? UnitNumber { get; set; }
    public string? ImmeubleName { get; set; }
    public Guid ProjectId { get; set; }
    public string? ProjectName { get; set; }

    public int? LastAttemptNo { get; set; }
    public DateTime? LastAppointmentStartsAt { get; set; }
    public string? LastAppointmentStatus { get; set; }

    public string? ReportStatus { get; set; }
    public int ActiveBlockingSnags { get; set; }
    public int ActiveMajorSnags { get; set; }
    public int ActiveMinorSnags { get; set; }
}

public class GetFinalVisitCasesHandler
    : IRequestHandler<GetFinalVisitCasesQuery, PaginatedResponse<FinalVisitCaseListItem>>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public GetFinalVisitCasesHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<PaginatedResponse<FinalVisitCaseListItem>> Handle(GetFinalVisitCasesQuery request, CancellationToken ct)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 500);
        var scope = await _projectScope.GetScopedProjectIdsAsync(ct);

        var rows =
            from c in _db.Set<FinalVisitCase>()
            join r in _db.Set<Reservation>() on c.ReservationId equals r.Id
            join u in _db.Set<UnitEntity>() on r.UnitId equals u.Id
            join im in _db.Set<Immeuble>() on u.ProjectId equals im.Id // Unit.ProjectId holds the immeuble id
            join p in _db.Set<Project>() on im.ProjectId equals p.Id
            select new { c, r, u, im, p };

        if (scope is not null) rows = rows.Where(x => scope.Contains(x.p.Id));
        if (request.ProjectId is Guid projectId) rows = rows.Where(x => x.p.Id == projectId);
        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<FinalVisitCaseStatus>(request.Status, ignoreCase: true, out var status))
        {
            rows = rows.Where(x => x.c.Status == status);
        }

        var total = await rows.LongCountAsync(ct);

        var page = await rows
            .OrderByDescending(x => x.c.OpenedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.c.Id,
                x.c.ReservationId,
                x.c.Status,
                x.c.OpenedAt,
                Buyer = ((x.r.Name ?? "") + " " + (x.r.LastName ?? "")).Trim(),
                UnitId = x.u.Id,
                x.u.UnitNumber,
                ImmeubleName = x.im.Name,
                ProjectId = x.p.Id,
                ProjectName = x.p.Name,
            })
            .ToListAsync(ct);

        var caseIds = page.Select(x => x.Id).ToList();

        var appointments = await _db.Set<FinalVisitAppointment>()
            .Where(a => caseIds.Contains(a.CaseId))
            .Select(a => new { a.Id, a.CaseId, a.AttemptNo, a.StartsAt, a.Status })
            .ToListAsync(ct);
        var appointmentIds = appointments.Select(a => a.Id).ToList();

        // Same "current report" rule as NotaryEligibilityService: latest non-superseded version.
        var reports = await _db.Set<FinalVisitReport>()
            .Where(r => appointmentIds.Contains(r.AppointmentId) && r.Status != ReportStatus.Superseded)
            .Select(r => new { r.Id, r.AppointmentId, r.Status, r.SubmittedAt, r.VersionNo })
            .ToListAsync(ct);
        var reportIds = reports.Select(r => r.Id).ToList();

        var snags = await _db.Set<Snag>()
            .Where(s => reportIds.Contains(s.ReportId))
            .Select(s => new { s.ReportId, s.Severity, s.Status })
            .ToListAsync(ct);

        var items = page.Select(x =>
        {
            var caseAppointments = appointments.Where(a => a.CaseId == x.Id).ToList();
            var last = caseAppointments.OrderByDescending(a => a.AttemptNo).FirstOrDefault();
            var caseAppointmentIds = caseAppointments.Select(a => a.Id).ToHashSet();
            var report = reports
                .Where(r => caseAppointmentIds.Contains(r.AppointmentId))
                .OrderByDescending(r => r.SubmittedAt)
                .ThenByDescending(r => r.VersionNo)
                .FirstOrDefault();
            var active = report is null
                ? new List<SnagSeverity>()
                : snags.Where(s => s.ReportId == report.Id && SnagStateMachine.IsActive(s.Status)).Select(s => s.Severity).ToList();

            return new FinalVisitCaseListItem
            {
                CaseId = x.Id,
                ReservationId = x.ReservationId,
                Status = x.Status.ToString(),
                OpenedAt = x.OpenedAt,
                BuyerName = string.IsNullOrWhiteSpace(x.Buyer) ? null : x.Buyer,
                UnitId = x.UnitId,
                UnitNumber = x.UnitNumber,
                ImmeubleName = x.ImmeubleName,
                ProjectId = x.ProjectId,
                ProjectName = x.ProjectName,
                LastAttemptNo = last?.AttemptNo,
                LastAppointmentStartsAt = last?.StartsAt,
                LastAppointmentStatus = last?.Status.ToString(),
                ReportStatus = report?.Status.ToString(),
                ActiveBlockingSnags = active.Count(s => s == SnagSeverity.Blocking),
                ActiveMajorSnags = active.Count(s => s == SnagSeverity.Major),
                ActiveMinorSnags = active.Count(s => s == SnagSeverity.Minor),
            };
        }).ToList();

        return new PaginatedResponse<FinalVisitCaseListItem>(items, pageNumber, pageSize, total);
    }
}
