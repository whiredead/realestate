using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Domain.Users.Entities;
using System.Linq.Expressions;

namespace ProjectAPI.Api.Application.Appointments.GetAppointments;

/// <summary>
/// Handler for getting the list of appointments based on filters.
/// </summary>
/// <remarks>
/// This handler processes the <see cref="GetAppointmentsQuery" /> to retrieve a list of appointments that match the specified filters.
/// It queries the appointment repository with the given filtering criteria and includes related entities such as Project and Agent.
///
/// §6.4 — carried no project scope at all: a SALES_AGENT with no filters saw
/// every appointment in every project, including colleagues' (GetAppointmentById
/// already restricted a SALES_AGENT to their own row; this list never did). A
/// caller-supplied ProjectId filter was also trusted unchecked. Both are now
/// intersected with the caller's own scope, and SALES_AGENT is additionally
/// pinned to their own SalesAgentId regardless of what AgentId was requested —
/// same convention as GetReservationsHandler.
/// </remarks>
public class GetAppointmentsHandler : IRequestHandler<GetAppointmentsQuery, PaginatedResponse<AppointmentResponse>>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetAppointmentsHandler" /> class.
    /// </summary>
    /// <param name="appointmentRepository">The repository to access appointment data.</param>
    /// <param name="projectScope">Enforces §6.4 project-perimeter scoping.</param>
    /// <param name="currentUser">The authenticated caller.</param>
    public GetAppointmentsHandler(
        IAppointmentRepository appointmentRepository,
        ProjectScopeService projectScope,
        ICurrentUser currentUser)
    {
        _appointmentRepository = appointmentRepository;
        _projectScope = projectScope;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Handles the query to get a list of appointments based on filters.
    /// </summary>
    /// <param name="request">The <see cref="GetAppointmentsQuery" /> containing filtering criteria such as ProjectId, AgentId, AppointmentDate, etc.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation, containing a list of <see cref="AppointmentResponse" />.</returns>
    public async Task<PaginatedResponse<AppointmentResponse>> Handle(GetAppointmentsQuery request, CancellationToken cancellationToken)
    {
        Expression<Func<Appointment, bool>> filter = null;

        var includes = new Expression<Func<Appointment, object>>[]
        {
                a=>a.Project,
                a => a.Agent
        };

        // §6.4 — null (GLOBAL_ADMIN) means unrestricted; otherwise the
        // caller's own scoped project ids, intersected with any requested
        // ProjectId filter rather than trusting it outright.
        var scopedProjectIds = await _projectScope.GetScopedProjectIdsAsync(cancellationToken);

        // A SALES_AGENT sees only their own appointments — a supplied AgentId
        // is ignored for them, same as GetReservationsHandler's convention.
        var effectiveAgentId = _currentUser.IsInRole(RoleCodes.SalesAgent)
            ? _currentUser.UserId
            : request.AgentId;

        var appointments = await _appointmentRepository.Find(a =>
                  (scopedProjectIds == null || scopedProjectIds.Contains(a.ProjectId)) &&
                  (!request.ProjectId.HasValue || a.ProjectId == request.ProjectId.Value) &&
                  (string.IsNullOrEmpty(effectiveAgentId) || a.SalesAgentId == effectiveAgentId) &&
                  (!request.AppointmentDate.HasValue || a.AppointmentDate.Date == request.AppointmentDate.Value.Date) &&
                  (string.IsNullOrEmpty(request.UserId) || a.UserId == request.UserId) &&
                  (string.IsNullOrEmpty(request.Status) || a.Status == request.Status) &&
                  (string.IsNullOrEmpty(request.Name) || a.Name.Contains(request.Name)) &&
                  (string.IsNullOrEmpty(request.LastName) || a.LastName.Contains(request.LastName)) &&
                  (string.IsNullOrEmpty(request.Email) || a.Email.Contains(request.Email)) &&
                  (string.IsNullOrEmpty(request.PhoneNumber) || a.PhoneNumber.Contains(request.PhoneNumber)), includes);


        var totalItems = appointments.Count();
        var paginatedData = appointments
            // Stable order before paging: without it page contents are
            // nondeterministic and rows repeat or vanish between pages.
            .OrderByDescending(a => a.AppointmentDate).ThenBy(a => a.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new AppointmentResponse
            {
                Id = a.Id,
                ProjectId = a.ProjectId,
                TypeBienIds = a.TypeBienIds,
                AgentId = a.SalesAgentId,
                PropertyType = a.PropertyType,
                // SalesAgentId is nullable (a public visitor request can be created
                // with no agent yet assigned) — a.Agent is then null after the
                // LEFT JOIN, and this threw NullReferenceException for every
                // caller until the SalesAgentId==null row was reached.
                AgentFullName = a.Agent != null ? a.Agent.FirstName + " " + a.Agent.LastName : null,
                ProjectName = a.Project.Name,
                AppointmentDate = a.AppointmentDate,
                UserId = a.UserId != null ? Guid.Parse(a.UserId) : null,
                Status = a.Status,
                Name = a.Name,
                LastName = a.LastName,
                Email = a.Email,
                PhoneNumber = a.PhoneNumber,
                Notes = a.Notes,
                CreatedAt = a.CreatedAt,
                AssignmentSource = a.AssignmentSource,
                AssignedAt = a.AssignedAt,
                PreviousSalesAgentId = a.PreviousSalesAgentId,
                ReassignmentReason = a.ReassignmentReason,
                PreviousAppointmentId = a.PreviousAppointmentId
            }).ToList();

        return new PaginatedResponse<AppointmentResponse>(paginatedData, request.PageNumber, request.PageSize, totalItems);
    }
}