using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Appointments.GetAppointmentVisitReport;

public class GetAppointmentVisitReportHandler : IRequestHandler<GetAppointmentVisitReportQuery, AppointmentVisitReportDto?>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;
    private readonly ApplicationDbContext _db;

    public GetAppointmentVisitReportHandler(
        IAppointmentRepository appointmentRepository,
        ProjectScopeService projectScope,
        ICurrentUser currentUser,
        ApplicationDbContext db)
    {
        _appointmentRepository = appointmentRepository;
        _projectScope = projectScope;
        _currentUser = currentUser;
        _db = db;
    }

    public async Task<AppointmentVisitReportDto?> Handle(GetAppointmentVisitReportQuery request, CancellationToken ct)
    {
        var appointment = await _appointmentRepository.GetByIDAsync(request.AppointmentId)
            ?? throw new NotFoundException($"Appointment {request.AppointmentId} not found.");

        await _projectScope.EnsureProjectAccessAsync(appointment.ProjectId, ct);

        // Reading follows the project perimeter checked above.

        var report = await _db.Set<AppointmentVisitReport>()
            .Where(r => r.AppointmentId == request.AppointmentId)
            .OrderByDescending(r => r.VersionNo)
            .FirstOrDefaultAsync(ct);

        if (report == null) return null;

        return new AppointmentVisitReportDto
        {
            Id = report.Id,
            AppointmentId = report.AppointmentId,
            VersionNo = report.VersionNo,
            PropertiesPresentedUnitIds = report.PropertiesPresentedUnitIds,
            InterestLevel = report.InterestLevel,
            ConfirmedBudget = report.ConfirmedBudget,
            ConfirmedRequirements = report.ConfirmedRequirements,
            Objections = report.Objections,
            NextAction = report.NextAction,
            FollowUpDate = report.FollowUpDate,
            VisitResult = report.VisitResult,
            InternalNotes = report.InternalNotes,
            AuthorUserId = report.AuthorUserId,
            CreatedAt = report.CreatedAt
        };
    }
}
