using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.PublicCatalogue;

/// <summary>
/// §7.2 — the option lists the public catalogue's filters need, and nothing else.
///
/// The listings page used to populate its project and quartier dropdowns from
/// <c>GET /api/Projects</c>. That returns the internal <c>ProjectResponse</c>,
/// which carries <c>AssignedAgents</c> and <c>AssignedNotaries</c> — names,
/// e-mail addresses, phone numbers and user ids — so every anonymous visitor
/// who opened the catalogue was handed the staff directory to render two
/// <c>&lt;select&gt;</c> elements.
///
/// This returns three flat lists. There is no navigation property on any of
/// them, so no entity graph can follow one out through the serialiser.
/// </summary>
public class GetPublicFiltersQuery : IRequest<PublicFilterOptions>
{
}

public class PublicFilterOption
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Lets the UI group or subtitle a project by its neighbourhood.</summary>
    public string? QuartierName { get; set; }
}

public class PublicFilterOptions
{
    public List<PublicFilterOption> Projects { get; set; } = new();
    public List<string> Quartiers { get; set; } = new();
    public List<string> PropertyTypes { get; set; } = new();
}

public class GetPublicFiltersHandler : IRequestHandler<GetPublicFiltersQuery, PublicFilterOptions>
{
    private readonly ApplicationDbContext _db;

    public GetPublicFiltersHandler(ApplicationDbContext db) => _db = db;

    public async Task<PublicFilterOptions> Handle(GetPublicFiltersQuery request, CancellationToken ct)
    {
        // Same visibility rule as the catalogue itself: a DRAFT project is not
        // on the market, so offering it as a filter would produce a named
        // option that always returns zero results.
        var projects = await _db.Set<Project>()
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new PublicFilterOption
            {
                Id = p.Id,
                Name = p.Name,
                QuartierName = p.Quartier != null ? p.Quartier.Name : null
            })
            .ToListAsync(ct);

        var quartiers = projects
            .Select(p => p.QuartierName)
            .Where(q => !string.IsNullOrWhiteSpace(q))
            .Select(q => q!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(q => q, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        // Derived from the plans actually on offer rather than from a fixed
        // list, so the filter never advertises a category with nothing behind
        // it — and a new category appears the moment stock exists for it.
        var plans = await PublicCatalogueProjection.BuildPlansAsync(_db, null, ct);

        var propertyTypes = plans
            .Select(p => p.PropertyType)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new PublicFilterOptions
        {
            Projects = projects,
            Quartiers = quartiers,
            PropertyTypes = propertyTypes
        };
    }
}
