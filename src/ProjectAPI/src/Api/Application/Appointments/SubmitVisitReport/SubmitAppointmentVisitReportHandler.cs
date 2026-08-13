using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Appointments.SubmitVisitReport;

/// <summary>
/// Records the sales agent's follow-up report after a commercial
/// appointment's visit (the missing link between "appointment happened" and
/// "reservation created" — see stage 3 of the buyer journey).
/// </summary>
public class SubmitAppointmentVisitReportHandler
    : IRequestHandler<SubmitAppointmentVisitReportCommand, SubmitAppointmentVisitReportResponse>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IAppointmentVisitReportRepository _reportRepository;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;
    private readonly ApplicationDbContext _db;

    public SubmitAppointmentVisitReportHandler(
        IAppointmentRepository appointmentRepository,
        IAppointmentVisitReportRepository reportRepository,
        ProjectScopeService projectScope,
        ICurrentUser currentUser,
        ApplicationDbContext db)
    {
        _appointmentRepository = appointmentRepository;
        _reportRepository = reportRepository;
        _projectScope = projectScope;
        _currentUser = currentUser;
        _db = db;
    }

    public async Task<SubmitAppointmentVisitReportResponse> Handle(SubmitAppointmentVisitReportCommand request, CancellationToken ct)
    {
        var appointment = await _appointmentRepository.GetByIDAsync(request.AppointmentId)
            ?? throw new NotFoundException($"Appointment {request.AppointmentId} not found.");

        await _projectScope.EnsureProjectAccessAsync(appointment.ProjectId, ct);

        // Same ownership rule as UpdateAppointmentStatusHandler: a
        // SALES_AGENT only reports on their OWN appointments, never a
        // colleague's — the visit report is that agent's account of what
        // happened, not something another agent can write on their behalf.
        if (_currentUser.IsInRole(RoleCodes.SalesAgent) && appointment.SalesAgentId != _currentUser.UserId)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.Unauthorized,
                "Ce rendez-vous n'est pas affecté à votre compte.",
                StatusCodes.Status403Forbidden);
        }

        if (!VisitInterestLevelCodes.All.Contains(request.InterestLevel))
        {
            throw new Common.Exceptions.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure(nameof(request.InterestLevel),
                    $"InterestLevel must be one of: {string.Join(", ", VisitInterestLevelCodes.All)}.")
            });
        }

        if (!AppointmentVisitResultCodes.All.Contains(request.VisitResult))
        {
            throw new Common.Exceptions.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure(nameof(request.VisitResult),
                    $"VisitResult must be one of: {string.Join(", ", AppointmentVisitResultCodes.All)}.")
            });
        }

        // A correction creates a new version rather than editing a submitted
        // one — same reasoning as FinalVisitReport: the prior version stays
        // exactly as it was recorded at the time.
        var lastVersion = await _db.Set<AppointmentVisitReport>()
            .Where(r => r.AppointmentId == request.AppointmentId)
            .OrderByDescending(r => r.VersionNo)
            .Select(r => (int?)r.VersionNo)
            .FirstOrDefaultAsync(ct);

        var report = new AppointmentVisitReport
        {
            Id = Guid.NewGuid(),
            AppointmentId = request.AppointmentId,
            VersionNo = (lastVersion ?? 0) + 1,
            PropertiesPresentedUnitIds = request.PropertiesPresentedUnitIds,
            InterestLevel = request.InterestLevel,
            ConfirmedBudget = request.ConfirmedBudget,
            ConfirmedRequirements = request.ConfirmedRequirements,
            Objections = request.Objections,
            NextAction = request.NextAction,
            FollowUpDate = request.FollowUpDate,
            VisitResult = request.VisitResult,
            InternalNotes = request.InternalNotes,
            AuthorUserId = request.AuthorUserId ?? _currentUser.UserId ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        await _reportRepository.InsertAsync(report);
        await _reportRepository.SaveAsync();

        return new SubmitAppointmentVisitReportResponse
        {
            Id = report.Id,
            VersionNo = report.VersionNo,
            Message = "Compte-rendu de visite enregistré."
        };
    }
}
