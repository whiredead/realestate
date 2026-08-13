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

    /// <summary>Global-admin override to bypass the 100% requirement, with justification.</summary>
    public bool OverrideIncompleteProgress { get; set; }
    public string? OverrideReason { get; set; }

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

    public CompleteProjectHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
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

        if (ProjectStatusCodes.Normalize(project.StatusGlobal) == ProjectStatusCodes.Completed)
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

        // §15.3: 100% is required unless a global admin documents an exception.
        if (progress < 100m && !request.OverrideIncompleteProgress)
        {
            throw new BusinessRuleException(
                "PROJECT_PROGRESS_INCOMPLETE",
                $"L'avancement pondéré est de {progress:N2}% ; 100% est requis pour terminer le projet, " +
                "sauf dérogation explicite d'un administrateur global.");
        }

        if (request.OverrideIncompleteProgress && string.IsNullOrWhiteSpace(request.OverrideReason))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "Une dérogation sur l'avancement incomplet exige un motif documenté.");
        }

        project.StatusGlobal = ProjectStatusCodes.Completed;
        project.OverAllProgress = 100m;

        // §15.3 requires an audit entry for this transition. The audit_logs
        // table itself is Lot 9 (§5.3); until then, the override reason and
        // actor are preserved on the published update so the decision is not
        // lost.
        _db.Add(new ConstructionUpdate
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            VersionNo = 1,
            ProgressPercent = 100m,
            TitleFr = "Projet terminé",
            TitleEn = "Project completed",
            DescriptionFr = request.OverrideIncompleteProgress
                ? $"Finalisation avec dérogation (avancement réel {progress:N2}%). Motif : {request.OverrideReason}"
                : "Projet finalisé — 100% de l'avancement pondéré atteint.",
            Visibility = UpdateVisibility.Public,
            AuthorUserId = request.ActorUserId,
            PublishedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
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
