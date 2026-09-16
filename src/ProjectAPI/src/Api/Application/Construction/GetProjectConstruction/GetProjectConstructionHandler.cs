using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Construction.GetProjectConstruction;

public class GetProjectConstructionHandler
    : IRequestHandler<GetProjectConstructionQuery, GetProjectConstructionResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public GetProjectConstructionHandler(ApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<GetProjectConstructionResponse> Handle(GetProjectConstructionQuery request, CancellationToken ct)
    {
        // §6.3 — this route is intentionally anonymous-reachable ("L publié"
        // pour le visiteur), so VisibleToPublic/VisibleToBuyer/Visibility must
        // be enforced HERE rather than assumed satisfied by [Authorize]: an
        // internal role sees everything, a signed-in buyer additionally sees
        // buyer-only content, and an anonymous/PROSPECT caller sees only what
        // is actually marked public. This was previously computed but never
        // filtered on, so any visitor could see buyer-only and internal-only
        // progress data regardless of the flags.
        var isInternal = _currentUser.IsAuthenticated
            && RoleCodes.Internal.Any(_currentUser.IsInRole);
        var isSignedIn = _currentUser.IsAuthenticated;

        var milestonesQuery = _db.Set<ConstructionMilestone>().Where(m => m.ProjectId == request.ProjectId);
        if (!isInternal)
        {
            milestonesQuery = isSignedIn
                ? milestonesQuery.Where(m => m.VisibleToPublic || m.VisibleToBuyer)
                : milestonesQuery.Where(m => m.VisibleToPublic);
        }

        var milestones = await milestonesQuery
            .OrderBy(m => m.SequenceNo)
            .Select(m => new MilestoneDto
            {
                Id = m.Id,
                Code = m.Code,
                NameFr = m.NameFr,
                NameEn = m.NameEn,
                SequenceNo = m.SequenceNo,
                WeightPercent = m.WeightPercent,
                PlannedDate = m.PlannedDate,
                ActualDate = m.ActualDate,
                Status = m.Status.ToString(),
                IsValidated = m.IsValidated,
                VisibleToBuyer = m.VisibleToBuyer,
                VisibleToPublic = m.VisibleToPublic
            })
            .ToListAsync(ct);

        // Superseded versions are excluded: a correction creates a new version
        // rather than editing the published one (§15.2 FR-CON-004).
        var updatesQuery = _db.Set<ConstructionUpdate>().Where(u => u.ProjectId == request.ProjectId);
        if (!isInternal)
        {
            updatesQuery = isSignedIn
                ? updatesQuery.Where(u => u.Visibility == UpdateVisibility.Public || u.Visibility == UpdateVisibility.Buyers)
                : updatesQuery.Where(u => u.Visibility == UpdateVisibility.Public);
        }

        var updates = await updatesQuery
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new UpdateDto
            {
                Id = u.Id,
                VersionNo = u.VersionNo,
                ProgressPercent = u.ProgressPercent,
                TitleFr = u.TitleFr,
                DescriptionFr = u.DescriptionFr,
                MediaUrls = u.MediaUrls,
                Visibility = u.Visibility.ToString(),
                PublishedAt = u.PublishedAt,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync(ct);

        // Overall progress is DERIVED from completed milestone weights (§5.8),
        // not read from a stored field that could drift.
        var totalWeight = milestones.Sum(m => m.WeightPercent);
        var doneWeight = milestones
            .Where(m => m.IsValidated && m.Status == MilestoneStatus.Completed.ToString())
            .Sum(m => m.WeightPercent);

        var progress = totalWeight > 0 ? Math.Round(doneWeight / totalWeight * 100, 2) : 0;

        return new GetProjectConstructionResponse
        {
            ProjectId = request.ProjectId,
            Milestones = milestones,
            Updates = updates,
            ComputedProgressPercent = progress,
            CalculatedAt = DateTime.UtcNow
        };
    }
}
