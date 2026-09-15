using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;
using UnitEntity = ProjectAPI.Domain.Immeubles.Entities.Unit;

namespace ProjectAPI.Api.Application.PublicCatalogue;

/// <summary>
/// §7.2 — the public plan catalogue: every TypeBien offered by every visible
/// project, filtered and paged server-side.
///
/// This replaces the browser doing the job: the listings page used to load 100
/// projects, then 100 buildings, then issue one units request PER BUILDING, and
/// filter the result in JavaScript. That is N+1 over the network from a public
/// page, and it published raw unit records to do it.
///
/// A catalogue row is a PLAN, not a building and not a unit. That is the whole
/// point — a visitor shops for "a 2-bedroom at Beaulieu from 900k", not for
/// "unit A-304 on floor 3 of building A".
/// </summary>
public class GetPublicPlansQuery : IRequest<PublicPlanPage>
{
    public Guid? ProjectId { get; set; }
    public string? Quartier { get; set; }

    /// <summary>Commercial category: Studio, Appartement, Bureau, Commerce.</summary>
    public string? PropertyType { get; set; }

    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? MinSurface { get; set; }
    public int? MaxSurface { get; set; }
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }

    /// <summary>AVAILABLE | LAST_UNITS | UNAVAILABLE.</summary>
    public string? Availability { get; set; }

    /// <summary>Free-text over plan name, project name and quartier.</summary>
    public string? Search { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 24;
}

public class GetPublicPlansHandler : IRequestHandler<GetPublicPlansQuery, PublicPlanPage>
{
    private readonly ApplicationDbContext _db;

    public GetPublicPlansHandler(ApplicationDbContext db) => _db = db;

    public async Task<PublicPlanPage> Handle(GetPublicPlansQuery request, CancellationToken ct)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, 60);
        var pageNumber = Math.Max(request.PageNumber, 1);

        var rows = await PublicCatalogueProjection.BuildPlansAsync(_db, request.ProjectId, ct);

        var filtered = rows.Where(plan => Matches(plan, request)).ToList();

        return new PublicPlanPage
        {
            TotalItems = filtered.Count,
            PageNumber = pageNumber,
            PageSize = pageSize,
            Data = filtered
                // Cheapest first: a catalogue sorted by "à partir de" is what a
                // shopper is actually scanning for. Plans with no price sink to
                // the bottom rather than pretending to be free.
                .OrderBy(p => p.StartingPrice ?? decimal.MaxValue)
                .ThenBy(p => p.ProjectName)
                .ThenBy(p => p.PlanName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList()
        };
    }

    private static bool Matches(PublicPlanSummary plan, GetPublicPlansQuery q)
    {
        if (q.ProjectId.HasValue && plan.ProjectId != q.ProjectId.Value) return false;

        if (!string.IsNullOrWhiteSpace(q.Quartier) &&
            !string.Equals(plan.QuartierName, q.Quartier, StringComparison.OrdinalIgnoreCase)) return false;

        if (!string.IsNullOrWhiteSpace(q.PropertyType) &&
            !string.Equals(plan.PropertyType, q.PropertyType, StringComparison.OrdinalIgnoreCase)) return false;

        // Price filters compare against the ONE number the card shows ("à
        // partir de"). Range-overlap semantics would match a plan whose
        // displayed price sits outside the band the visitor chose — the card
        // and the filter would visibly disagree.
        if (q.MinPrice.HasValue && (plan.StartingPrice ?? 0) < q.MinPrice.Value) return false;
        if (q.MaxPrice.HasValue && (plan.StartingPrice ?? decimal.MaxValue) > q.MaxPrice.Value) return false;

        // Surface IS a range on the card, so overlap is the honest test here.
        if (q.MinSurface.HasValue && (plan.MaxSurface ?? plan.MinSurface ?? int.MaxValue) < q.MinSurface.Value) return false;
        if (q.MaxSurface.HasValue && (plan.MinSurface ?? plan.MaxSurface ?? 0) > q.MaxSurface.Value) return false;

        if (q.Bedrooms.HasValue && plan.Bedrooms != q.Bedrooms.Value) return false;
        if (q.Bathrooms.HasValue && plan.Bathrooms != q.Bathrooms.Value) return false;

        if (!string.IsNullOrWhiteSpace(q.Availability) &&
            !string.Equals(plan.Availability, q.Availability, StringComparison.OrdinalIgnoreCase)) return false;

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var needle = q.Search.Trim();
            var haystack = $"{plan.PlanName} {plan.ProjectName} {plan.QuartierName} {plan.Location}";
            if (!haystack.Contains(needle, StringComparison.OrdinalIgnoreCase)) return false;
        }

        return true;
    }
}

/// <summary>
/// Builds plan rows from projects, their TypeBiens and their available stock.
/// Shared by the catalogue and the project-detail page so a plan card is
/// identical in both places.
/// </summary>
public static class PublicCatalogueProjection
{
    public static async Task<List<PublicPlanSummary>> BuildPlansAsync(
        ApplicationDbContext db, Guid? projectId, CancellationToken ct)
    {
        var projectsQuery = db.Set<Project>()
            .Include(p => p.Quartier)
            .Include(p => p.TypeBiens).ThenInclude(ptb => ptb.TypeBien)
            .AsNoTracking()
            .AsQueryable();

        if (projectId.HasValue)
        {
            projectsQuery = projectsQuery.Where(p => p.Id == projectId.Value);
        }
        else
        {
            // Every status — sur plan, en livraison, finalisé — is catalogue
            // content: a finished development still needs a public page.
        }

        var projects = await projectsQuery.ToListAsync(ct);
        if (projects.Count == 0) return new List<PublicPlanSummary>();

        var projectIds = projects.Select(p => p.Id).ToList();

        // One query for every unit across every project in scope, rather than
        // one request per building. Only the four columns the projection needs
        // leave the database — no unit record is ever materialised whole.
        var units = await (
            from u in db.Set<UnitEntity>().AsNoTracking()
            join im in db.Set<Immeuble>().AsNoTracking() on u.ProjectId equals im.Id
            where projectIds.Contains(im.ProjectId)
            select new
            {
                RealProjectId = im.ProjectId,
                u.NumberOfBedrooms,
                u.LatestPrice,
                u.Status
            }).ToListAsync(ct);

        var unitsByProject = units.GroupBy(u => u.RealProjectId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var plans = new List<PublicPlanSummary>();

        foreach (var project in projects)
        {
            var phase = ProjectStatusCodes.GetBusinessPhase(project.StatusGlobal);
            unitsByProject.TryGetValue(project.Id, out var projectUnits);
            projectUnits ??= new();

            foreach (var link in project.TypeBiens.Where(t => t.TypeBien is not null))
            {
                var type = link.TypeBien!;

                // A Unit carries no TypeBienId — the model has no join between
                // the two. Bedroom count is the only signal linking a plan to
                // the stock that realises it, and it is what the catalogue has
                // always matched on. Documented rather than hidden: a real
                // Unit.TypeBienId would make this exact.
                var matching = type.NbrChambre is null
                    ? projectUnits
                    : projectUnits.Where(u => u.NumberOfBedrooms == type.NbrChambre).ToList();

                var availableUnits = matching
                    .Where(u => u.Status == UnitCommercialStatus.Available)
                    .ToList();

                // The plan's own price wins; the cheapest available unit is the
                // fallback, so a plan with no configured price still shows a
                // real "à partir de" instead of nothing.
                var startingPrice = type.Price.HasValue
                    ? (decimal)type.Price.Value
                    : availableUnits.Where(u => u.LatestPrice.HasValue)
                        .Select(u => u.LatestPrice!.Value)
                        .DefaultIfEmpty(0m)
                        .Min();

                plans.Add(new PublicPlanSummary
                {
                    TypeBienId = type.Id,
                    ProjectId = project.Id,
                    PlanName = type.Name,
                    Description = type.Description,
                    PropertyType = ClassifyPropertyType(type.Name, project.Type),
                    ProjectName = project.Name,
                    QuartierName = project.Quartier?.Name,
                    Location = project.Location,
                    Phase = phase,
                    StartingPrice = startingPrice > 0 ? startingPrice : null,
                    MinSurface = type.MinSurface,
                    MaxSurface = type.MaxSurface,
                    Bedrooms = type.NbrChambre,
                    Bathrooms = type.NbrSalleDeBain,
                    Showers = type.NbrDouche,
                    Parking = type.NbrParking,
                    CoverImage = FirstImage(type.Image) ?? project.Images.FirstOrDefault(),
                    Availability = PublicAvailability.FromCount(availableUnits.Count),
                    Module3DLink = string.IsNullOrWhiteSpace(type.Module3DLink) ? null : type.Module3DLink
                });
            }
        }

        return plans;
    }

    /// <summary>
    /// Maps a plan onto the four commercial categories the public site filters
    /// by. The referential has no category column, so this reads the plan's own
    /// name first and falls back to the project's type — which is how the
    /// listings page has always inferred it, just done once here instead of in
    /// every component.
    /// </summary>
    public static string ClassifyPropertyType(string? planName, string? projectType)
    {
        var text = $"{planName} {projectType}".ToLowerInvariant();

        if (text.Contains("studio")) return "Studio";
        if (text.Contains("bureau")) return "Bureau";
        // "commercial" does not contain "commerce" — the substrings diverge at
        // the seventh letter — so a plan named "Local commercial" was falling
        // through to Appartement and the Commerce filter stayed empty.
        if (text.Contains("commerce") || text.Contains("commercial") ||
            text.Contains("magasin") || text.Contains("boutique")) return "Commerce";
        return "Appartement";
    }

    /// <summary>TypeBien.Image is a single column that sometimes holds a comma-separated list.</summary>
    public static string? FirstImage(string? packed) =>
        string.IsNullOrWhiteSpace(packed)
            ? null
            : packed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
}
