using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.FinalVisits.GetMyFinalVisitReport;

public class GetMyFinalVisitReportHandler : IRequestHandler<GetMyFinalVisitReportQuery, MyFinalVisitReportDto?>
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public GetMyFinalVisitReportHandler(ApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<MyFinalVisitReportDto?> Handle(GetMyFinalVisitReportQuery request, CancellationToken ct)
    {
        var reservation = await _db.Set<Reservation>().FindAsync(new object?[] { request.ReservationId }, ct)
            ?? throw new NotFoundException($"Reservation {request.ReservationId} not found.");

        // FinalVisitReport carries no buyer id of its own — ownership is proven
        // through the reservation its case belongs to (§17.1).
        if (string.IsNullOrEmpty(reservation.BuyerId) || reservation.BuyerId != _currentUser.UserId)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.Unauthorized,
                "Ce compte-rendu n'est pas accessible.",
                StatusCodes.Status403Forbidden);
        }

        var visitCase = await _db.Set<FinalVisitCase>()
            .Include(c => c.Appointments)
            .FirstOrDefaultAsync(c => c.ReservationId == request.ReservationId, ct);

        if (visitCase == null) return null;

        var appointmentIds = visitCase.Appointments.Select(a => a.Id).ToList();

        var report = await _db.Set<FinalVisitReport>()
            .Include(r => r.Snags)
            .Where(r => appointmentIds.Contains(r.AppointmentId) && r.Status != ReportStatus.Superseded)
            .OrderByDescending(r => r.SubmittedAt)
            .ThenByDescending(r => r.VersionNo)
            .FirstOrDefaultAsync(ct);

        if (report == null) return null;

        return new MyFinalVisitReportDto
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
            Snags = report.Snags.Select(s => new MySnagDto
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
