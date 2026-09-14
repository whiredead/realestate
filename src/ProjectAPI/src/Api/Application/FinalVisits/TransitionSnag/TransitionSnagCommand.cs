using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.FinalVisits.TransitionSnag;

/// <summary>
/// Moves a snag through its lifecycle (spec §17.4 FR-FVI-011):
/// OPEN → ACKNOWLEDGED → IN_RESOLUTION → RESOLVED → VALIDATED → CLOSED,
/// with RESOLVED → IN_RESOLUTION when the proof is rejected.
///
/// This matters for the notary gate: a major or blocking snag stays ACTIVE
/// until VALIDATED, so merely marking it RESOLVED does not unblock anything.
/// </summary>
public class TransitionSnagCommand : IRequest<TransitionSnagResponse>
{
    public Guid SnagId { get; set; }

    public SnagStatus TargetStatus { get; set; }

    /// <summary>Required when moving to RESOLVED (§17.4).</summary>
    public string? ResolutionComment { get; set; }

    /// <summary>Proof of resolution, when the configuration demands it.</summary>
    public string? ProofUrl { get; set; }

    public string? ActorUserId { get; set; }
    public string? Comment { get; set; }
}

public class TransitionSnagResponse
{
    public Guid SnagId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool StillBlocksNotary { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class TransitionSnagHandler : IRequestHandler<TransitionSnagCommand, TransitionSnagResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;

    public TransitionSnagHandler(ApplicationDbContext db, ProjectScopeService projectScope, ICurrentUser currentUser)
    {
        _db = db;
        _projectScope = projectScope;
        _currentUser = currentUser;
    }

    public async Task<TransitionSnagResponse> Handle(TransitionSnagCommand request, CancellationToken ct)
    {
        var snag = await _db.Set<Snag>()
            .FirstOrDefaultAsync(s => s.Id == request.SnagId, ct)
            ?? throw new NotFoundException($"Snag {request.SnagId} not found.");

        // §6.4 — the agent/admin resolving this snag must be scoped to its project.
        var reservationId = await (
            from r in _db.Set<FinalVisitReport>()
            join a in _db.Set<FinalVisitAppointment>() on r.AppointmentId equals a.Id
            join c in _db.Set<FinalVisitCase>() on a.CaseId equals c.Id
            where r.Id == snag.ReportId
            select c.ReservationId).FirstOrDefaultAsync(ct);

        if (reservationId == Guid.Empty)
            throw BusinessRuleException.ProjectScopeDenied(reservationId);

        await _projectScope.EnsureReservationAccessAsync(reservationId, ct);

        if (!SnagStateMachine.CanTransition(snag.Status, request.TargetStatus))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.InvalidStatusTransition,
                $"Transition de réserve non autorisée : {snag.Status} → {request.TargetStatus}.",
                StatusCodes.Status409Conflict);
        }

        // §17.4: declaring a snag resolved requires a description of what was done.
        if (request.TargetStatus == SnagStatus.Resolved && string.IsNullOrWhiteSpace(request.ResolutionComment))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "Une description de la résolution est obligatoire pour passer la réserve à RESOLVED.");
        }

        var previous = snag.Status;
        snag.Status = request.TargetStatus;

        if (!string.IsNullOrWhiteSpace(request.ResolutionComment))
        {
            snag.ResolutionComment = request.ResolutionComment;
        }

        if (!string.IsNullOrWhiteSpace(request.ProofUrl))
        {
            snag.ProofUrl = request.ProofUrl;
        }

        _db.Add(new SnagHistory
        {
            Id = Guid.NewGuid(),
            Snag = snag,
            FromStatus = previous,
            ToStatus = request.TargetStatus,
            ActorUserId = _currentUser.UserId,
            OccurredAt = DateTime.UtcNow,
            Comment = request.Comment ?? request.ResolutionComment
        });

        await _db.SaveChangesAsync(ct);

        var stillActive = SnagStateMachine.IsActive(snag.Status);
        var blocksNotary = stillActive && snag.Severity is SnagSeverity.Major or SnagSeverity.Blocking;

        return new TransitionSnagResponse
        {
            SnagId = snag.Id,
            Status = snag.Status.ToString(),
            StillBlocksNotary = blocksNotary,
            Message = blocksNotary
                ? $"Réserve {snag.Severity} toujours active : le rendez-vous notarial reste bloqué jusqu'à validation."
                : "Réserve mise à jour."
        };
    }
}
