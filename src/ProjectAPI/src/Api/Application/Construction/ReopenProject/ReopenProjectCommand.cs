using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Construction.ReopenProject;

/// <summary>
/// Takes a FINALISED project back to EN_LIVRAISON. Finalising is deliberately permanent (the project becomes
/// read-only), so this is a rare, GLOBAL_ADMIN-only correction for a finalisation done by mistake. It needs a written
/// reason and leaves an internal audit entry (who, when, why).
/// </summary>
public class ReopenProjectCommand : IRequest<ReopenProjectResponse>
{
    public Guid ProjectId { get; set; }

    public string? Reason { get; set; }
}

public class ReopenProjectResponse
{
    public Guid ProjectId { get; set; }
    public string Status { get; set; } = ProjectStatusCodes.EnLivraison;
    public string Message { get; set; } = string.Empty;
}

public class ReopenProjectHandler : IRequestHandler<ReopenProjectCommand, ReopenProjectResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ReopenProjectHandler(ApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ReopenProjectResponse> Handle(ReopenProjectCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsGlobalAdmin)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.Unauthorized,
                "Seul un administrateur global peut rouvrir un projet finalisé.",
                StatusCodes.Status403Forbidden);
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "Rouvrir un projet finalisé exige un motif documenté.");
        }

        var project = await _db.Set<Project>().AsNoTracking()
            .Where(p => p.Id == request.ProjectId)
            .Select(p => new { p.Id, p.StatusGlobal, p.OverAllProgress })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Project {request.ProjectId} not found.");

        if (ProjectStatusCodes.Normalize(project.StatusGlobal) != ProjectStatusCodes.Finalise)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.InvalidStatusTransition,
                "Ce projet n'est pas finalisé : il n'y a rien à rouvrir.",
                StatusCodes.Status409Conflict);
        }

        // The write lock refuses tracked changes to a finalised project, so the status is changed directly in the
        // database; from then on the project is no longer read-only and the audit entry below saves normally.
        await _db.Set<Project>()
            .Where(p => p.Id == request.ProjectId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StatusGlobal, ProjectStatusCodes.EnLivraison), ct);

        var actorUserId = _currentUser.UserId;
        var now = DateTime.UtcNow;
        _db.Add(new ConstructionUpdate
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            VersionNo = 1,
            ProgressPercent = project.OverAllProgress,
            TitleFr = "Projet rouvert",
            TitleEn = "Project reopened",
            DescriptionFr = $"Projet finalisé rouvert (FINALISE → EN_LIVRAISON) par {actorUserId ?? "?"} le {now:yyyy-MM-dd HH:mm} UTC. Motif : {request.Reason.Trim()}",
            Visibility = UpdateVisibility.Internal,
            AuthorUserId = actorUserId,
            PublishedAt = now,
            CreatedAt = now
        });
        await _db.SaveChangesAsync(ct);

        return new ReopenProjectResponse
        {
            ProjectId = request.ProjectId,
            Message = "Projet rouvert : il est de nouveau en livraison et modifiable."
        };
    }
}
