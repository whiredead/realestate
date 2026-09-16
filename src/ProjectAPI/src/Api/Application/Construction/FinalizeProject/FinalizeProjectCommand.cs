using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Construction.FinalizeProject;

/// <summary>
/// Second step of the project lifecycle: EN_LIVRAISON (COMPLETED) → FINALISE (ARCHIVED).
///
/// <see cref="CompleteProject.CompleteProjectCommand"/> closes the build at 100 %
/// and opens delivery; this closes the project itself once delivery is over.
/// A finalised project is read-only (<see cref="ProjectStatusCodes.IsReadOnly"/>).
/// The same 100 % weighted-progress gate applies, with the same GLOBAL_ADMIN-only
/// derogation, so a project completed under derogation cannot be silently
/// finalised by a project admin.
/// </summary>
public class FinalizeProjectCommand : IRequest<FinalizeProjectResponse>
{
    public Guid ProjectId { get; set; }

    public bool Confirm { get; set; }

    public bool OverrideIncompleteProgress { get; set; }

    public string? OverrideReason { get; set; }
}

public class FinalizeProjectResponse
{
    public Guid ProjectId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string BusinessPhase { get; set; } = string.Empty;
    public decimal ComputedProgressPercent { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class FinalizeProjectHandler : IRequestHandler<FinalizeProjectCommand, FinalizeProjectResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;

    public FinalizeProjectHandler(ApplicationDbContext db, ProjectScopeService projectScope, ICurrentUser currentUser)
    {
        _db = db;
        _projectScope = projectScope;
        _currentUser = currentUser;
    }

    public async Task<FinalizeProjectResponse> Handle(FinalizeProjectCommand request, CancellationToken ct)
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

        var phase = ProjectStatusCodes.GetBusinessPhase(project.StatusGlobal);
        if (phase != ProjectStatusCodes.Phase.EnLivraison)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.InvalidStatusTransition,
                $"Seul un projet en livraison peut être finalisé (phase actuelle : {phase}).",
                StatusCodes.Status409Conflict);
        }

        var milestones = await _db.Set<ConstructionMilestone>()
            .Where(m => m.ProjectId == request.ProjectId)
            .ToListAsync(ct);
        var totalWeight = milestones.Sum(m => m.WeightPercent);
        var doneWeight = milestones.Where(m => m.IsValidated && m.Status == MilestoneStatus.Completed).Sum(m => m.WeightPercent);
        var progress = totalWeight > 0 ? Math.Round(doneWeight / totalWeight * 100, 2) : 0m;

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
                $"L'avancement pondéré est de {progress:N2}% ; 100% est requis pour finaliser le projet, " +
                "sauf dérogation explicite d'un administrateur global.");
        }

        var decidedAt = DateTime.UtcNow;
        var actorUserId = _currentUser.UserId;

        project.StatusGlobal = ProjectStatusCodes.Finalise;

        // Same audit carrier as CompleteProject until the audit_logs table exists.
        _db.Add(new ConstructionUpdate
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            VersionNo = 1,
            ProgressPercent = progress,
            TitleFr = "Projet finalisé",
            TitleEn = "Project finalised",
            DescriptionFr = request.OverrideIncompleteProgress
                ? $"Finalisation avec dérogation par {actorUserId ?? "?"} le {decidedAt:yyyy-MM-dd HH:mm} UTC " +
                  $"(avancement pondéré réel {progress:N2}%). Motif : {request.OverrideReason}"
                : $"Projet finalisé par {actorUserId ?? "?"} le {decidedAt:yyyy-MM-dd HH:mm} UTC — " +
                  "phase précédente : EN_LIVRAISON. Le projet est désormais en lecture seule.",
            Visibility = UpdateVisibility.Internal,
            AuthorUserId = actorUserId,
            PublishedAt = decidedAt,
            CreatedAt = decidedAt
        });

        await _db.SaveChangesAsync(ct);

        return new FinalizeProjectResponse
        {
            ProjectId = project.Id,
            Status = ProjectStatusCodes.Finalise,
            BusinessPhase = ProjectStatusCodes.Phase.Finalise,
            ComputedProgressPercent = progress,
            Message = "Projet finalisé : il est désormais en lecture seule."
        };
    }
}
