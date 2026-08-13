using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Appointments.GetAppointmentVisitReport;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Appointments.GetAppointmentVisitReportForBuyer;

public class GetAppointmentVisitReportForBuyerHandler
    : IRequestHandler<GetAppointmentVisitReportForBuyerQuery, AppointmentVisitReportBuyerDto?>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ApplicationDbContext _db;

    public GetAppointmentVisitReportForBuyerHandler(
        IAppointmentRepository appointmentRepository,
        ICurrentUser currentUser,
        ApplicationDbContext db)
    {
        _appointmentRepository = appointmentRepository;
        _currentUser = currentUser;
        _db = db;
    }

    public async Task<AppointmentVisitReportBuyerDto?> Handle(GetAppointmentVisitReportForBuyerQuery request, CancellationToken ct)
    {
        var appointment = await _appointmentRepository.GetByIDAsync(request.AppointmentId)
            ?? throw new NotFoundException($"Appointment {request.AppointmentId} not found.");

        // A buyer only ever sees their OWN appointment's report — never
        // another prospect's, and never the internal projection's private fields.
        if (string.IsNullOrEmpty(appointment.UserId) || appointment.UserId != _currentUser.UserId)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.Unauthorized,
                "Ce compte-rendu n'est pas accessible.",
                StatusCodes.Status403Forbidden);
        }

        var report = await _db.Set<AppointmentVisitReport>()
            .Where(r => r.AppointmentId == request.AppointmentId)
            .OrderByDescending(r => r.VersionNo)
            .FirstOrDefaultAsync(ct);

        if (report == null) return null;

        return new AppointmentVisitReportBuyerDto
        {
            Id = report.Id,
            VersionNo = report.VersionNo,
            PropertiesPresentedUnitIds = report.PropertiesPresentedUnitIds,
            NextAction = report.NextAction,
            FollowUpDate = report.FollowUpDate,
            CreatedAt = report.CreatedAt
        };
    }
}
