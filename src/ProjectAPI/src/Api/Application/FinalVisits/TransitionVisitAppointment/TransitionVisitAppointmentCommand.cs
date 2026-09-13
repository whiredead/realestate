using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.FinalVisits.TransitionVisitAppointment;

/// <summary>
/// Drives a final-visit attempt through the shared appointment matrix
/// (spec §17.2 FR-FVI-003, §47.3): confirm, propose a new slot, reject, cancel,
/// mark completed or mark the buyer absent.
/// </summary>
public class TransitionVisitAppointmentCommand : IRequest<TransitionVisitAppointmentResponse>
{
    public Guid AppointmentId { get; set; }

    public AppointmentAttemptStatus TargetStatus { get; set; }

    /// <summary>New slot, required when proposing a reschedule.</summary>
    public DateTime? ProposedStartsAt { get; set; }
    public DateTime? ProposedEndsAt { get; set; }

    /// <summary>Motive for rejection or cancellation.</summary>
    public string? Reason { get; set; }

    public string? ActorUserId { get; set; }

    /// <summary>
    /// Lets a project admin record a visit as completed before its start time
    /// (§17.2 FR-FVI-003 allows this only as an audited exception).
    /// </summary>
    public bool AllowEarlyCompletion { get; set; }
}

public class TransitionVisitAppointmentResponse
{
    public Guid AppointmentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class TransitionVisitAppointmentHandler
    : IRequestHandler<TransitionVisitAppointmentCommand, TransitionVisitAppointmentResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;

    public TransitionVisitAppointmentHandler(ApplicationDbContext db, ProjectScopeService projectScope, ICurrentUser currentUser)
    {
        _db = db;
        _projectScope = projectScope;
        _currentUser = currentUser;
    }

    public async Task<TransitionVisitAppointmentResponse> Handle(
        TransitionVisitAppointmentCommand request,
        CancellationToken ct)
    {
        var appointment = await _db.Set<FinalVisitAppointment>()
            .Include(a => a.Case)
            .FirstOrDefaultAsync(a => a.Id == request.AppointmentId, ct)
            ?? throw new NotFoundException($"Final visit appointment {request.AppointmentId} not found.");

        // §6.4 — agent/admin handling this attempt must be scoped to its project.
        await _projectScope.EnsureReservationAccessAsync(appointment.Case.ReservationId, ct);

        if (!AppointmentStateMachine.CanTransition(appointment.Status, request.TargetStatus))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.InvalidStatusTransition,
                $"Transition non autorisée : {appointment.Status} → {request.TargetStatus}.",
                StatusCodes.Status409Conflict);
        }

        // §17.2: the agent cannot mark a visit done before it starts, unless a
        // project admin explicitly authorises it.
        if (request.TargetStatus == AppointmentAttemptStatus.Completed
            && appointment.StartsAt > DateTime.UtcNow
            && (!request.AllowEarlyCompletion
                || (!_currentUser.IsInRole(RoleCodes.ProjectAdmin) && !_currentUser.IsGlobalAdmin)))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "Une visite ne peut pas être déclarée réalisée avant son heure de début.");
        }

        if (request.TargetStatus == AppointmentAttemptStatus.RescheduleProposed)
        {
            if (request.ProposedStartsAt is null)
            {
                throw new BusinessRuleException(
                    BusinessErrorCodes.ValidationFailed,
                    "Une proposition de report exige un nouveau créneau.");
            }

            // The proposal replaces the slot in place; the buyer must accept it
            // before the attempt becomes CONFIRMED (§17.2).
            appointment.StartsAt = request.ProposedStartsAt.Value;
            appointment.EndsAt = request.ProposedEndsAt ?? request.ProposedStartsAt.Value.AddHours(1);
        }

        if (request.TargetStatus is AppointmentAttemptStatus.Rejected or AppointmentAttemptStatus.Cancelled
            && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "Un motif est obligatoire pour rejeter ou annuler une visite.");
        }

        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            appointment.CauseDescription = request.Reason;
        }

        appointment.Status = request.TargetStatus;

        await _db.SaveChangesAsync(ct);

        return new TransitionVisitAppointmentResponse
        {
            AppointmentId = appointment.Id,
            Status = appointment.Status.ToString(),
            Message = request.TargetStatus switch
            {
                AppointmentAttemptStatus.Confirmed => "Visite finale confirmée.",
                AppointmentAttemptStatus.RescheduleProposed => "Nouveau créneau proposé, en attente d'acceptation par l'acheteur.",
                AppointmentAttemptStatus.Completed => "Visite réalisée. Le compte rendu peut être saisi.",
                AppointmentAttemptStatus.NoShow => "Acheteur absent.",
                _ => "Statut mis à jour."
            }
        };
    }
}
