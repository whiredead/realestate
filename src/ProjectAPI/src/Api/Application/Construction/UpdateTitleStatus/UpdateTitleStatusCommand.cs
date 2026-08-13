using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Construction.UpdateTitleStatus;

/// <summary>
/// Moves a unit's land-title status (spec §16 FR-TITLE-001).
///
/// Forward moves follow the fixed path; a backward move is permitted but
/// requires a reason and is recorded in the history, per the spec.
/// </summary>
public class UpdateTitleStatusCommand : IRequest<UpdateTitleStatusResponse>
{
    public Guid UnitId { get; set; }

    /// <summary>Target status: NotAvailable, InProgress, Available, DeliveredToNotary, Completed.</summary>
    public TitleStatus Status { get; set; }

    /// <summary>Mandatory when moving backwards (§16).</summary>
    public string? Reason { get; set; }

    public string? DocumentUrl { get; set; }
    public string? ActorUserId { get; set; }
}

public class UpdateTitleStatusResponse
{
    public Guid UnitId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PreviousStatus { get; set; }
    public bool AllowsNotaryAppointment { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class UpdateTitleStatusHandler : IRequestHandler<UpdateTitleStatusCommand, UpdateTitleStatusResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public UpdateTitleStatusHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<UpdateTitleStatusResponse> Handle(UpdateTitleStatusCommand request, CancellationToken ct)
    {
        // Unit.ProjectId is actually the FK to Immeuble (Building) — the real
        // Project id is Immeuble.ProjectId (see Domain\Immeubles\Entities\Unit.cs).
        var unitProjectId = await _db.Set<Domain.Immeubles.Entities.Unit>()
            .Where(u => u.Id == request.UnitId)
            .Select(u => (Guid?)u.Immeuble.ProjectId)
            .FirstOrDefaultAsync(ct);

        if (unitProjectId is null)
        {
            throw new NotFoundException($"Unit {request.UnitId} not found.");
        }

        await _projectScope.EnsureProjectAccessAsync(unitProjectId.Value, ct);

        var state = await _db.Set<UnitTitleState>()
            .FirstOrDefaultAsync(t => t.UnitId == request.UnitId, ct);

        var previous = state?.Status;

        if (previous == request.Status)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.InvalidStatusTransition,
                $"Le titre est déjà au statut {request.Status}.",
                StatusCodes.Status409Conflict);
        }

        // §16: going backwards is allowed, but never silently.
        if (previous is not null && TitleStateMachine.IsBackward(previous.Value, request.Status)
            && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "Un retour en arrière du statut de titre exige un motif.");
        }

        if (state is null)
        {
            state = new UnitTitleState
            {
                Id = Guid.NewGuid(),
                UnitId = request.UnitId
            };
            _db.Add(state);
        }

        state.Status = request.Status;
        state.StatusAt = DateTime.UtcNow;
        state.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.DocumentUrl))
        {
            state.DocumentUrl = request.DocumentUrl;
        }

        // Every change is traced (§16 FR-TITLE-001).
        _db.Add(new UnitTitleHistory
        {
            Id = Guid.NewGuid(),
            UnitId = request.UnitId,
            FromStatus = previous,
            ToStatus = request.Status,
            ActorUserId = request.ActorUserId,
            OccurredAt = DateTime.UtcNow,
            Reason = request.Reason,
            DocumentUrl = request.DocumentUrl
        });

        await _db.SaveChangesAsync(ct);

        return new UpdateTitleStatusResponse
        {
            UnitId = request.UnitId,
            Status = request.Status.ToString(),
            PreviousStatus = previous?.ToString(),
            AllowsNotaryAppointment = TitleStateMachine.AllowsNotaryAppointment(request.Status),
            Message = request.Status == TitleStatus.Available
                // §16 FR-TITLE-002 requires notifying buyer and agent here; the
                // notification module (Lot 9) is not built yet.
                ? "Titre disponible. L'acheteur et l'agent doivent être notifiés."
                : "Statut du titre mis à jour."
        };
    }
}
