using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Appointments.UpdateAppointmentStatus;

/// <summary>
/// Drives a commercial appointment through the shared §47.3 matrix (the same
/// one final-visit, notary and handover appointments obey) instead of writing
/// any string verbatim (§7: direct status updates are forbidden).
///
/// Terminal statuses (COMPLETED/REJECTED/CANCELLED/NO_SHOW) end the record —
/// a retry is a NEW appointment linked via <see cref="Appointment.PreviousAppointmentId"/>,
/// never a rewrite of this one (§5.1/§10.3).
/// </summary>
public class UpdateAppointmentStatusHandler : IRequestHandler<UpdateAppointmentStatusCommand, UpdateAppointmentStatusResponse>
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IAppointmentAssignmentHistoryRepository _historyRepository;
    private readonly IProjectMembershipRepository _membershipRepository;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;
    private readonly ApplicationDbContext _db;

    public UpdateAppointmentStatusHandler(
        IAppointmentRepository appointmentRepository,
        IAppointmentAssignmentHistoryRepository historyRepository,
        IProjectMembershipRepository membershipRepository,
        ProjectScopeService projectScope,
        ICurrentUser currentUser,
        ApplicationDbContext db)
    {
        _appointmentRepository = appointmentRepository;
        _historyRepository = historyRepository;
        _membershipRepository = membershipRepository;
        _projectScope = projectScope;
        _currentUser = currentUser;
        _db = db;
    }

    public async Task<UpdateAppointmentStatusResponse> Handle(UpdateAppointmentStatusCommand request, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetByIDAsync(request.AppointmentId);
        if (appointment == null)
        {
            throw new NotFoundException($"Appointment with ID {request.AppointmentId} not found.");
        }

        // §6.4 — the agent/admin handling this appointment must be scoped to
        // its project.
        await _projectScope.EnsureProjectAccessAsync(appointment.ProjectId, cancellationToken);

        // Project-level scope only proves this agent works on the project —
        // not that this specific appointment is theirs. Without this, any
        // SALES_AGENT assigned to the project could update or reassign a
        // colleague's appointment. Admins are exempt (they oversee the whole
        // project's agenda by design).
        if (_currentUser.IsInRole(RoleCodes.SalesAgent) && appointment.SalesAgentId != _currentUser.UserId)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.Unauthorized,
                "Ce rendez-vous n'est pas affecté à votre compte.",
                StatusCodes.Status403Forbidden);
        }

        if (!Enum.TryParse<AppointmentAttemptStatus>(appointment.Status, out var currentStatus))
        {
            // Legacy free-text value predating this machine (e.g. the old
            // "En cours de traitement"). Treat it as REQUESTED, the state every
            // appointment starts in, rather than failing closed on old data.
            currentStatus = AppointmentAttemptStatus.Requested;
        }

        if (!Enum.TryParse<AppointmentAttemptStatus>(request.Status, true, out var targetStatus))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                $"Statut de rendez-vous inconnu : {request.Status}.");
        }

        if (currentStatus != targetStatus && !AppointmentStateMachine.CanTransition(currentStatus, targetStatus))
        {
            throw BusinessRuleException.InvalidStatusTransition(currentStatus.ToString(), targetStatus.ToString());
        }

        // §10.1 — a reschedule proposal replaces the slot in place; the
        // requester must accept it before the attempt becomes CONFIRMED.
        if (targetStatus == AppointmentAttemptStatus.RescheduleProposed)
        {
            if (request.ProposedDate is null)
            {
                throw new BusinessRuleException(
                    BusinessErrorCodes.ValidationFailed,
                    "Une proposition de report exige un nouveau créneau.");
            }

            appointment.AppointmentDate = request.ProposedDate.Value;
        }

        if (targetStatus is AppointmentAttemptStatus.Rejected or AppointmentAttemptStatus.Cancelled
            && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "Un motif est obligatoire pour rejeter ou annuler un rendez-vous.");
        }

        if (!string.IsNullOrWhiteSpace(request.NewSalesAgentId))
        {
            if (string.IsNullOrWhiteSpace(request.ReassignmentReason))
            {
                throw new BusinessRuleException(
                    BusinessErrorCodes.ValidationFailed,
                    "Un motif est obligatoire pour réaffecter un rendez-vous.");
            }

            await EnsureEligibleSalesAgentAsync(appointment.ProjectId, request.NewSalesAgentId, cancellationToken);
            await EnsureSlotAvailableAsync(request.NewSalesAgentId, appointment.AppointmentDate, cancellationToken);

            var previousAgentId = appointment.SalesAgentId;

            if (currentStatus == AppointmentAttemptStatus.Confirmed)
            {
                // Never silently change a confirmed appointment: freeze this
                // row (Superseded) and create a new one carrying the
                // reassignment, pending the prospect's acceptance — the same
                // machinery an ordinary reschedule-after-confirmation uses.
                appointment.Status = AppointmentAttemptStatus.Superseded.ToString();
                await _appointmentRepository.Update(appointment);

                var replacement = new Appointment
                {
                    Id = Guid.NewGuid(),
                    ProjectId = appointment.ProjectId,
                    SalesAgentId = request.NewSalesAgentId,
                    AssignmentSource = "MANUAL_REASSIGNMENT",
                    AssignedAt = DateTime.UtcNow,
                    AssignedByUserId = request.ActorUserId,
                    PreviousSalesAgentId = previousAgentId,
                    ReassignmentReason = request.ReassignmentReason,
                    AppointmentDate = request.ProposedDate ?? appointment.AppointmentDate,
                    PreviousAppointmentId = appointment.Id,
                    TypeBienIds = appointment.TypeBienIds,
                    PropertyType = appointment.PropertyType,
                    UserId = appointment.UserId,
                    CrmContactId = appointment.CrmContactId,
                    Name = appointment.Name,
                    Email = appointment.Email,
                    PhoneNumber = appointment.PhoneNumber,
                    Notes = appointment.Notes,
                    LastName = appointment.LastName,
                    Status = AppointmentAttemptStatus.Requested.ToString(),
                };

                await _appointmentRepository.InsertAsync(replacement);
                await _historyRepository.InsertAsync(new AppointmentAssignmentHistory
                {
                    Id = Guid.NewGuid(),
                    AppointmentId = replacement.Id,
                    SalesAgentId = request.NewSalesAgentId,
                    PreviousSalesAgentId = previousAgentId,
                    AssignmentSource = "MANUAL_REASSIGNMENT",
                    Reason = request.ReassignmentReason,
                    ActorUserId = request.ActorUserId,
                    AssignedAt = DateTime.UtcNow
                });

                await _appointmentRepository.SaveAsync();

                return new UpdateAppointmentStatusResponse
                {
                    AppointmentId = replacement.Id,
                    Status = replacement.Status,
                    Message = "Rendez-vous réaffecté ; en attente d'acceptation du nouveau créneau par le prospect."
                };
            }

            // Not yet confirmed: nothing was promised to the prospect, so an
            // in-place change is safe.
            appointment.PreviousSalesAgentId = previousAgentId;
            appointment.SalesAgentId = request.NewSalesAgentId;
            appointment.AssignmentSource = "MANUAL_REASSIGNMENT";
            appointment.AssignedAt = DateTime.UtcNow;
            appointment.AssignedByUserId = request.ActorUserId;
            appointment.ReassignmentReason = request.ReassignmentReason;

            await _historyRepository.InsertAsync(new AppointmentAssignmentHistory
            {
                Id = Guid.NewGuid(),
                AppointmentId = appointment.Id,
                SalesAgentId = request.NewSalesAgentId,
                PreviousSalesAgentId = previousAgentId,
                AssignmentSource = "MANUAL_REASSIGNMENT",
                Reason = request.ReassignmentReason,
                ActorUserId = request.ActorUserId,
                AssignedAt = DateTime.UtcNow
            });
        }

        // Update the appointment status
        appointment.Status = targetStatus.ToString();
        await _appointmentRepository.Update(appointment);
        await _appointmentRepository.SaveAsync();

        // Create response
        return new UpdateAppointmentStatusResponse
        {
            AppointmentId = appointment.Id,
            Status = appointment.Status,
            Message = targetStatus switch
            {
                AppointmentAttemptStatus.Confirmed => "Rendez-vous confirmé.",
                AppointmentAttemptStatus.RescheduleProposed => "Nouveau créneau proposé, en attente d'acceptation.",
                AppointmentAttemptStatus.Completed => "Rendez-vous réalisé.",
                AppointmentAttemptStatus.NoShow => "Absence constatée.",
                AppointmentAttemptStatus.Rejected => "Rendez-vous rejeté.",
                AppointmentAttemptStatus.Cancelled => "Rendez-vous annulé.",
                _ => "Statut mis à jour."
            }
        };
    }

    /// <summary>The new agent must actually hold an active SALES_AGENT membership on this exact project.</summary>
    private async Task EnsureEligibleSalesAgentAsync(Guid projectId, string agentId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var memberships = await _membershipRepository.Find(m =>
            m.ProjectId == projectId &&
            m.UserId == agentId &&
            m.RoleCode == RoleCodes.SalesAgent &&
            m.IsActive &&
            m.ValidFrom <= now &&
            (m.ValidUntil == null || m.ValidUntil > now));

        if (!memberships.Any())
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.AgentNotEligibleForProject,
                $"L'agent {agentId} n'a pas d'affectation SALES_AGENT active sur ce projet.");
        }
    }

    /// <summary>Same ±1-minute overlap check CreateAppointmentHandler applies — recalculated for the NEW agent, not the old one.</summary>
    private async Task EnsureSlotAvailableAsync(string agentId, DateTime appointmentDate, CancellationToken ct)
    {
        var blockingStatuses = AppointmentStateMachine.BlockingStatuses.Select(s => s.ToString()).ToArray();
        var conflictWindowStart = appointmentDate.AddMinutes(-1);
        var conflictWindowEnd = appointmentDate.AddMinutes(1);

        var hasConflict = await _db.Set<Appointment>().AnyAsync(a =>
            a.SalesAgentId == agentId &&
            blockingStatuses.Contains(a.Status) &&
            a.AppointmentDate > conflictWindowStart &&
            a.AppointmentDate < conflictWindowEnd, ct);

        if (hasConflict)
        {
            throw BusinessRuleException.AppointmentSlotConflict(appointmentDate);
        }
    }
}
