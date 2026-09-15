using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Api.Application.Appointments.GetAppointmentById;

public class GetAppointmentByIdHandler : IRequestHandler<GetAppointmentByIdQuery, AppointmentResponse>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ProjectScopeService _projectScope;

    public GetAppointmentByIdHandler(
        IAppointmentRepository appointmentRepository,
        ICurrentUser currentUser,
        ProjectScopeService projectScope)
    {
        _appointmentRepository = appointmentRepository;
        _currentUser = currentUser;
        _projectScope = projectScope;
    }

    public async Task<AppointmentResponse> Handle(GetAppointmentByIdQuery request, CancellationToken cancellationToken)
    {
        var results = await _appointmentRepository.Find(
            a => a.Id == request.AppointmentId,
            a => a.Project,
            a => a.Agent);
        var appointment = results.FirstOrDefault();

        if (appointment == null)
        {
            throw new NotFoundException($"Appointment with ID {request.AppointmentId} not found.");
        }

        // §6.4 — a PROJECT_ADMIN outside this appointment's project must not
        // read it, same as every other project-scoped mutation/read.
        await _projectScope.EnsureProjectAccessAsync(appointment.ProjectId, cancellationToken);

        // Same ownership rule as UpdateAppointmentStatusHandler: a
        // SALES_AGENT only sees their own appointments, never a colleague's.
        if (_currentUser.IsInRole(RoleCodes.SalesAgent) && appointment.SalesAgentId != _currentUser.UserId)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.Unauthorized,
                "Ce rendez-vous n'est pas affecté à votre compte.",
                StatusCodes.Status403Forbidden);
        }

        return new AppointmentResponse
        {
            Id = appointment.Id,
            ProjectId = appointment.ProjectId,
            TypeBienIds = appointment.TypeBienIds,
            AgentId = appointment.SalesAgentId,
            PropertyType = appointment.PropertyType,
            AgentFullName = appointment.Agent != null ? appointment.Agent.FirstName + " " + appointment.Agent.LastName : null,
            ProjectName = appointment.Project.Name,
            AppointmentDate = appointment.AppointmentDate,
            UserId = appointment.UserId != null ? Guid.Parse(appointment.UserId) : null,
            Status = appointment.Status,
            Name = appointment.Name,
            LastName = appointment.LastName,
            Email = appointment.Email,
            PhoneNumber = appointment.PhoneNumber,
            Notes = appointment.Notes,
            CreatedAt = appointment.CreatedAt,
            AssignmentSource = appointment.AssignmentSource,
            AssignedAt = appointment.AssignedAt,
            PreviousSalesAgentId = appointment.PreviousSalesAgentId,
            ReassignmentReason = appointment.ReassignmentReason,
            PreviousAppointmentId = appointment.PreviousAppointmentId
        };
    }
}
