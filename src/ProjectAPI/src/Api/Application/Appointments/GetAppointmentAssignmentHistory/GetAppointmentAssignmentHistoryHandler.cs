using Microsoft.AspNetCore.Identity;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Users.Interfaces;

namespace ProjectAPI.Api.Application.Appointments.GetAppointmentAssignmentHistory;

public class GetAppointmentAssignmentHistoryHandler
    : IRequestHandler<GetAppointmentAssignmentHistoryQuery, List<AppointmentAssignmentHistoryDto>>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IAppointmentAssignmentHistoryRepository _historyRepository;
    private readonly UserManager<User> _userManager;
    private readonly ProjectScopeService _projectScope;

    public GetAppointmentAssignmentHistoryHandler(
        IAppointmentRepository appointmentRepository,
        IAppointmentAssignmentHistoryRepository historyRepository,
        UserManager<User> userManager,
        ProjectScopeService projectScope)
    {
        _appointmentRepository = appointmentRepository;
        _historyRepository = historyRepository;
        _userManager = userManager;
        _projectScope = projectScope;
    }

    public async Task<List<AppointmentAssignmentHistoryDto>> Handle(GetAppointmentAssignmentHistoryQuery request, CancellationToken cancellationToken)
    {
        // Walk the PreviousAppointmentId chain back to the very first
        // booking, collecting every row id along the way — a post-
        // confirmation reassignment creates a NEW appointment row (see
        // UpdateAppointmentStatusHandler), so the full story spans multiple
        // Appointment ids, not just the one the caller asked about.
        var appointmentIds = new List<Guid>();
        var currentId = (Guid?)request.AppointmentId;
        var guard = 0;
        Guid? projectId = null;

        while (currentId.HasValue && guard++ < 50)
        {
            var appointment = await _appointmentRepository.GetByIDAsync(currentId.Value);
            if (appointment == null) break;

            appointmentIds.Add(currentId.Value);
            projectId ??= appointment.ProjectId;
            currentId = appointment.PreviousAppointmentId;
        }

        if (appointmentIds.Count == 0)
        {
            throw new NotFoundException($"Appointment with ID {request.AppointmentId} not found.");
        }

        // §6.4 — a PROJECT_ADMIN outside this appointment's project must not
        // read its assignment history. RestrictToAgentId (below) already
        // covers the SALES_AGENT ownership case.
        await _projectScope.EnsureProjectAccessAsync(projectId!.Value, cancellationToken);

        var allHistory = (await _historyRepository.Find(h => appointmentIds.Contains(h.AppointmentId)))
            .OrderBy(h => h.AssignedAt)
            .ToList();

        if (!string.IsNullOrWhiteSpace(request.RestrictToAgentId))
        {
            var involved = allHistory.Any(h =>
                h.SalesAgentId == request.RestrictToAgentId ||
                h.PreviousSalesAgentId == request.RestrictToAgentId);

            if (!involved)
            {
                // Same non-disclosure reasoning as ProjectScopeDenied: don't
                // distinguish "doesn't exist" from "exists but isn't yours".
                throw new BusinessRuleException(
                    BusinessErrorCodes.Unauthorized,
                    "Cet historique n'est pas accessible.",
                    StatusCodes.Status403Forbidden);
            }
        }

        var agentIds = allHistory
            .SelectMany(h => new[] { h.SalesAgentId, h.PreviousSalesAgentId })
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var agentNames = new Dictionary<string, string>();
        foreach (var agentId in agentIds)
        {
            var user = await _userManager.FindByIdAsync(agentId!);
            if (user != null)
            {
                agentNames[agentId!] = $"{user.FirstName} {user.LastName}";
            }
        }

        return allHistory
            .Select(h => new AppointmentAssignmentHistoryDto
            {
                Id = h.Id,
                SalesAgentId = h.SalesAgentId,
                SalesAgentFullName = h.SalesAgentId != null && agentNames.TryGetValue(h.SalesAgentId, out var name1) ? name1 : null,
                PreviousSalesAgentId = h.PreviousSalesAgentId,
                PreviousSalesAgentFullName = h.PreviousSalesAgentId != null && agentNames.TryGetValue(h.PreviousSalesAgentId, out var name2) ? name2 : null,
                AssignmentSource = h.AssignmentSource,
                Reason = h.Reason,
                ActorUserId = h.ActorUserId,
                AssignedAt = h.AssignedAt
            })
            .ToList();
    }
}
