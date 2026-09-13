using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.PublicCatalogue;

/// <summary>
/// §7.2 — one project as the public detail page needs it, in a single request.
///
/// There was no public by-id route at all: GET /api/Projects/{id} is
/// AdminsAgents-only, so the public page fetched a 1000-row list and searched
/// it in the browser, then made three more calls for features, quartier
/// amenities and videos. This replaces all four, and returns a projection with
/// no agents, no notaries, no immeubles, no units and no stock counts in it.
/// </summary>
public class GetPublicProjectQuery : IRequest<PublicProjectDetail>
{
    public Guid ProjectId { get; set; }
}

public class GetPublicProjectHandler : IRequestHandler<GetPublicProjectQuery, PublicProjectDetail>
{
    private readonly ApplicationDbContext _db;

    public GetPublicProjectHandler(ApplicationDbContext db) => _db = db;

    public async Task<PublicProjectDetail> Handle(GetPublicProjectQuery request, CancellationToken ct)
    {
        var project = await _db.Set<Project>()
            .Include(p => p.Quartier)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, ct)
            ?? throw new NotFoundException($"Project {request.ProjectId} not found.");

        // A DRAFT project is not on the market. 404 rather than 403: to an
        // anonymous visitor an unpublished project simply does not exist, and
        // saying "forbidden" would confirm that it does.
        if (ProjectStatusCodes.Normalize(project.StatusGlobal) == ProjectStatusCodes.Draft)
        {
            throw new NotFoundException($"Project {request.ProjectId} not found.");
        }

        var phase = ProjectStatusCodes.GetBusinessPhase(project.StatusGlobal);

        var plans = await PublicCatalogueProjection.BuildPlansAsync(_db, project.Id, ct);

        var amenities = await _db.Set<ProjectFeature>()
            .AsNoTracking()
            .Where(f => f.ProjectId == project.Id)
            .Select(f => new PublicFeature { Name = f.Name, Icon = f.Icon })
            .ToListAsync(ct);

        var quartierFeatures = await _db.Set<QuartierAmenity>()
            .AsNoTracking()
            .Where(a => a.ProjectId == project.Id)
            .Select(a => new PublicFeature { Name = a.Name, Icon = a.Icon })
            .ToListAsync(ct);

        var videos = await _db.Set<EspaceTempsReel>()
            .AsNoTracking()
            .Where(v => v.ProjectId == project.Id)
            .OrderByDescending(v => v.InsertedAt)
            .Select(v => v.VideoLink)
            .ToListAsync(ct);

        var prices = plans.Where(p => p.StartingPrice.HasValue).Select(p => p.StartingPrice!.Value).ToList();
        var minSurfaces = plans.Where(p => p.MinSurface.HasValue).Select(p => p.MinSurface!.Value).ToList();
        var maxSurfaces = plans.Where(p => p.MaxSurface.HasValue).Select(p => p.MaxSurface!.Value).ToList();

        return new PublicProjectDetail
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Location = project.Location,
            Address = project.Address,
            Phase = phase,

            // §15 — construction progress is only meaningful while the project
            // is still being built. Publishing "100%" next to a finished
            // development is noise, and publishing a figure for a suspended one
            // invites the question the page cannot answer.
            ConstructionProgress = phase == ProjectStatusCodes.Phase.SurPlan
                ? project.OverAllProgress
                : null,

            StartingPrice = prices.Count > 0 ? prices.Min() : null,
            MaxPrice = prices.Count > 0 ? prices.Max() : null,
            MinSurface = minSurfaces.Count > 0 ? minSurfaces.Min() : null,
            MaxSurface = maxSurfaces.Count > 0 ? maxSurfaces.Max() : null,

            Images = project.Images ?? new List<string>(),
            Videos = videos,
            Module3DLink = string.IsNullOrWhiteSpace(project.Module3DLink) ? null : project.Module3DLink,

            QuartierName = project.Quartier?.Name,
            QuartierDescription = project.Quartier?.Description,
            QuartierImages = SplitImages(project.Quartier?.Images),

            Amenities = amenities,
            QuartierFeatures = quartierFeatures,
            Plans = plans.OrderBy(p => p.StartingPrice ?? decimal.MaxValue).ThenBy(p => p.PlanName).ToList(),
            PropertyTypes = plans.Select(p => p.PropertyType).Distinct().OrderBy(t => t).ToList()
        };
    }

    /// <summary>Quartier.Images is one comma-separated column, mirroring Project.Images.</summary>
    private static List<string> SplitImages(string? packed) =>
        string.IsNullOrWhiteSpace(packed)
            ? new List<string>()
            : packed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}

/// <summary>
/// One plan, for the public plan-detail page. Same projection as a catalogue
/// card so the two can never describe the same plan differently.
/// </summary>
public class GetPublicPlanQuery : IRequest<PublicPlanDetail>
{
    public Guid ProjectId { get; set; }
    public int TypeBienId { get; set; }
}

public class PublicPlanDetail
{
    public PublicPlanSummary Plan { get; set; } = new();

    /// <summary>Interior photos of the layout, beyond the cover image.</summary>
    public List<string> Images { get; set; } = new();

    public string? ProjectDescription { get; set; }
    public string? QuartierDescription { get; set; }

    /// <summary>Falls back to the project's tour when the plan has none of its own.</summary>
    public string? ProjectModule3DLink { get; set; }
}

public class GetPublicPlanHandler : IRequestHandler<GetPublicPlanQuery, PublicPlanDetail>
{
    private readonly ApplicationDbContext _db;

    public GetPublicPlanHandler(ApplicationDbContext db) => _db = db;

    public async Task<PublicPlanDetail> Handle(GetPublicPlanQuery request, CancellationToken ct)
    {
        var project = await _db.Set<Project>()
            .Include(p => p.Quartier)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, ct)
            ?? throw new NotFoundException($"Project {request.ProjectId} not found.");

        if (ProjectStatusCodes.Normalize(project.StatusGlobal) == ProjectStatusCodes.Draft)
        {
            throw new NotFoundException($"Project {request.ProjectId} not found.");
        }

        var plans = await PublicCatalogueProjection.BuildPlansAsync(_db, project.Id, ct);

        var plan = plans.FirstOrDefault(p => p.TypeBienId == request.TypeBienId)
            ?? throw new NotFoundException(
                $"Plan {request.TypeBienId} is not offered by project {request.ProjectId}.");

        var type = await _db.Set<Domain.Immeubles.Entities.TypeBien>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TypeBienId, ct);

        // ImagesInterieur and Image are both comma-separated columns; the cover
        // is whichever came first, and the gallery is everything else.
        var gallery = Split(type?.ImagesInterieur).Concat(Split(type?.Image)).Distinct().ToList();

        return new PublicPlanDetail
        {
            Plan = plan,
            Images = gallery,
            ProjectDescription = project.Description,
            QuartierDescription = project.Quartier?.Description,
            ProjectModule3DLink = string.IsNullOrWhiteSpace(project.Module3DLink) ? null : project.Module3DLink
        };
    }

    private static IEnumerable<string> Split(string? packed) =>
        string.IsNullOrWhiteSpace(packed)
            ? Enumerable.Empty<string>()
            : packed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
