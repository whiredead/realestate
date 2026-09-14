using ProjectAPI.Api.Application.Common.Units;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.FinalVisits.GetFinalVisitCase;

public class GetFinalVisitCaseHandler : IRequestHandler<GetFinalVisitCaseQuery, FinalVisitCaseDto?>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public GetFinalVisitCaseHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<FinalVisitCaseDto?> Handle(GetFinalVisitCaseQuery request, CancellationToken ct)
    {
        // §6.4 — same perimeter as every other reservation-scoped write in this
        // module (RequestFinalVisit, TransitionAppointment, SubmitReport).
        await _projectScope.EnsureReservationAccessAsync(request.ReservationId, ct);

        var reservation = await _db.Set<Reservation>().FindAsync(new object?[] { request.ReservationId }, ct)
            ?? throw new NotFoundException($"Reservation {request.ReservationId} not found.");

        var visitCase = await _db.Set<FinalVisitCase>()
            .Include(c => c.Appointments)
            .FirstOrDefaultAsync(c => c.ReservationId == request.ReservationId, ct);

        if (visitCase is null) return null;

        var currentAppointment = visitCase.Appointments
            .OrderByDescending(a => a.AttemptNo)
            .FirstOrDefault();

        FinalVisitReportDto? currentReportDto = null;
        if (currentAppointment is not null)
        {
            var report = await _db.Set<FinalVisitReport>()
                .Include(r => r.Snags)
                .Where(r => r.AppointmentId == currentAppointment.Id)
                .OrderByDescending(r => r.VersionNo)
                .FirstOrDefaultAsync(ct);

            if (report is not null)
            {
                currentReportDto = new FinalVisitReportDto
                {
                    ReportId = report.Id,
                    VersionNo = report.VersionNo,
                    Status = (int)report.Status,
                    ResultCode = (int)report.ResultCode,
                    GeneralCondition = report.GeneralCondition,
                    Observations = report.Observations,
                    ClientFeedback = report.ClientFeedback,
                    NonComplianceReason = report.NonComplianceReason,
                    CorrectiveAction = report.CorrectiveAction,
                    FollowUpNotes = report.FollowUpNotes,
                    SubmittedAt = report.SubmittedAt,
                    AcknowledgedAt = report.AcknowledgedAt,
                    DisputeReason = report.DisputeReason,
                    Snags = report.Snags.Select(s => new FinalVisitSnagDto
                    {
                        Id = s.Id,
                        Code = s.Code,
                        CategoryCode = s.CategoryCode,
                        Severity = (int)s.Severity,
                        Description = s.Description,
                        Location = s.Location,
                        TargetResolutionDate = s.TargetResolutionDate,
                        Status = (int)s.Status
                    }).ToList()
                };
            }
        }

        var agentName = string.IsNullOrEmpty(visitCase.ResponsibleSalesAgentId)
            ? null
            : await _db.Users.Where(u => u.Id == visitCase.ResponsibleSalesAgentId)
                .Select(u => (u.FirstName + " " + u.LastName).Trim())
                .FirstOrDefaultAsync(ct);

        var location = (await Common.Units.UnitLocations.ForUnitsAsync(_db, new[] { reservation.UnitId }, ct)).GetValueOrDefault(reservation.UnitId);

        return new FinalVisitCaseDto
        {
            CaseId = visitCase.Id,
            Status = (int)visitCase.Status,
            ResponsibleSalesAgentId = visitCase.ResponsibleSalesAgentId,
            ResponsibleSalesAgentName = agentName,
            CurrentAppointment = currentAppointment is null ? null : new FinalVisitAppointmentDto
            {
                AppointmentId = currentAppointment.Id,
                AttemptNo = currentAppointment.AttemptNo,
                StartsAt = currentAppointment.StartsAt,
                EndsAt = currentAppointment.EndsAt,
                CauseType = currentAppointment.CauseType,
                CauseDescription = currentAppointment.CauseDescription,
                Status = (int)currentAppointment.Status
            },
            CurrentReport = currentReportDto
        }.WithLocation(location);
    }
}
