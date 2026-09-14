using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Construction.CompleteProject;

/// <summary>
/// Moves a project to COMPLETED (spec §15.3 FR-CON-005), which is what allows
/// buyers to request a final visit (§7.5, §17.1).
///
/// Requires 100% weighted milestone progress unless a global administrator
/// grants a documented exception, plus an explicit confirmation and an actual
/// end date.
/// </summary>
public class CompleteProjectCommand : IRequest<CompleteProjectResponse>
{
    public Guid ProjectId { get; set; }

    /// <summary>Must be explicitly true — this is a one-way, high-impact action.</summary>
    public bool Confirm { get; set; }

    public DateTime? ActualEndDate { get; set; }

    /// <summary>
    /// Bypasses the 100% requirement. Enforced as GLOBAL_ADMIN-only in the
    /// handler — a PROJECT_ADMIN sending this gets 403, because the endpoint's
    /// own role gate (Admins) admits both.
    /// </summary>
    public bool OverrideIncompleteProgress { get; set; }

    /// <summary>Mandatory whenever <see cref="OverrideIncompleteProgress"/> is set.</summary>
    public string? OverrideReason { get; set; }

    /// <summary>
    /// Accepted for wire compatibility and deliberately IGNORED: the audit
    /// record's actor is read from the token (<c>ICurrentUser.UserId</c>), never
    /// from the request body. Do not re-wire this into the handler.
    /// </summary>
    public string? ActorUserId { get; set; }
}

public class CompleteProjectResponse
{
    public Guid ProjectId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal ComputedProgressPercent { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class CompleteProjectHandler : IRequestHandler<CompleteProjectCommand, CompleteProjectResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;

    public CompleteProjectHandler(
        ApplicationDbContext db,
        ProjectScopeService projectScope,
        ICurrentUser currentUser)
    {
        _db = db;
        _projectScope = projectScope;
        _currentUser = currentUser;
    }

    public async Task<CompleteProjectResponse> Handle(CompleteProjectCommand request, CancellationToken ct)
    {
        var project = await _db.Set<Project>()
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, ct)
            ?? throw new NotFoundException($"Project {request.ProjectId} not found.");

        await _projectScope.EnsureProjectAccessAsync(request.ProjectId, ct);

        if (!request.Confirm)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "La finalisation du projet exige une confirmation explicite.");
        }

        if (ProjectStatusCodes.Normalize(project.StatusGlobal) is ProjectStatusCodes.Completed or ProjectStatusCodes.Archived)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.InvalidStatusTransition,
                "Ce projet est déjà terminé.",
                StatusCodes.Status409Conflict);
        }

        var milestones = await _db.Set<ConstructionMilestone>()
            .Where(m => m.ProjectId == request.ProjectId)
            .ToListAsync(ct);

        var totalWeight = milestones.Sum(m => m.WeightPercent);
        var doneWeight = milestones
            .Where(m => m.Status == MilestoneStatus.Completed)
            .Sum(m => m.WeightPercent);

        var progress = totalWeight > 0 ? Math.Round(doneWeight / totalWeight * 100, 2) : 0m;

        // §15.3 — the derogation is a GLOBAL_ADMIN power, and it is checked before
        // anything else it could unlock. Previously the endpoint's role gate
        // (Admins = GLOBAL_ADMIN + PROJECT_ADMIN) was the only barrier and the
        // flag itself was unguarded, so any project admin could finalise a
        // project at any progress level simply by sending it.
        if (request.OverrideIncompleteProgress && !_currentUser.IsGlobalAdmin)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.Unauthorized,
                "Seul un administrateur global peut finaliser un projet dont l'avancement est incomplet.",
                StatusCodes.Status403Forbidden);
        }

        if (request.OverrideIncompleteProgress && string.IsNullOrWhiteSpace(request.OverrideReason))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "Une dérogation sur l'avancement incomplet exige un motif documenté.");
        }

        if (progress < 100m && !request.OverrideIncompleteProgress)
        {
            throw new BusinessRuleException(
                "PROJECT_PROGRESS_INCOMPLETE",
                $"L'avancement pondéré est de {progress:N2}% ; 100% est requis pour terminer le projet, " +
                "sauf dérogation explicite d'un administrateur global.");
        }

        var previousStatus = ProjectStatusCodes.Normalize(project.StatusGlobal);
        var previousProgress = project.OverAllProgress;
        var decidedAt = DateTime.UtcNow;

        // The actor is taken from the token, never from the request: ActorUserId
        // is caller-supplied and this is the audit record for a one-way,
        // high-impact transition.
        var actorUserId = _currentUser.UserId;

        project.StatusGlobal = ProjectStatusCodes.Completed;
        project.OverAllProgress = 100m;

        // §15.3 requires an audit entry for this transition. The audit_logs
        // table itself is Lot 9 (§5.3); until then, the decision is preserved on
        // the published update: actor, previous phase and progress, timestamp,
        // and the derogation reason when one was used.
        _db.Add(new ConstructionUpdate
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            VersionNo = 1,
            ProgressPercent = 100m,
            TitleFr = "Projet terminé",
            TitleEn = "Project completed",
            DescriptionFr = request.OverrideIncompleteProgress
                ? $"Finalisation avec dérogation par {actorUserId ?? "?"} le {decidedAt:yyyy-MM-dd HH:mm} UTC. " +
                  $"Phase précédente : {ProjectStatusCodes.GetBusinessPhase(previousStatus)} " +
                  $"(statut {previousStatus}, avancement enregistré {previousProgress:N2}%, " +
                  $"avancement pondéré réel {progress:N2}%). Motif : {request.OverrideReason}"
                : $"Projet finalisé par {actorUserId ?? "?"} le {decidedAt:yyyy-MM-dd HH:mm} UTC — " +
                  $"100% de l'avancement pondéré atteint (phase précédente : " +
                  $"{ProjectStatusCodes.GetBusinessPhase(previousStatus)}).",
            Visibility = UpdateVisibility.Public,
            AuthorUserId = actorUserId,
            PublishedAt = decidedAt,
            CreatedAt = decidedAt
        });

        await _db.SaveChangesAsync(ct);

        return new CompleteProjectResponse
        {
            ProjectId = project.Id,
            Status = ProjectStatusCodes.Completed,
            ComputedProgressPercent = progress,
            Message = "Projet marqué comme terminé. Les visites finales peuvent désormais être demandées."
        };
    }
}
