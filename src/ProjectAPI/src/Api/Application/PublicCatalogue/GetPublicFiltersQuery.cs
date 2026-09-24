using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Immeubles.Entities;
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
    public string? QuartierCity { get; set; }
}

/// <summary>One row of the TypeBien referential, as the public site shows it.</summary>
public class PublicTypeBienOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Image { get; set; }
}

public class PublicFilterOptions
{
    public List<PublicFilterOption> Projects { get; set; } = new();
    public List<string> Quartiers { get; set; } = new();

    /// <summary>Distinct cities (villes) of the quartiers that host a project.</summary>
    public List<string> Cities { get; set; } = new();

    /// <summary>TypeBien names, the values the plans' PropertyType filter accepts.</summary>
    public List<string> PropertyTypes { get; set; } = new();

    /// <summary>The same TypeBien rows with their photo, for the site's category tiles and menus.</summary>
    public List<PublicTypeBienOption> TypeBiens { get; set; } = new();
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
                QuartierName = p.Quartier != null ? p.Quartier.Name : null,
                QuartierCity = p.Quartier != null ? p.Quartier.City : null
            })
            .ToListAsync(ct);

        var quartiers = projects
            .Select(p => p.QuartierName)
            .Where(q => !string.IsNullOrWhiteSpace(q))
            .Select(q => q!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(q => q, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var cities = projects
            .Select(p => p.QuartierCity)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        // The property types are the TypeBien referential itself: a type an admin
        // adds on the Types de biens page appears on the public site at once.
        var typeBiens = (await _db.Set<TypeBien>()
                .AsNoTracking()
                .Where(t => t.Name != null && t.Name != "")
                .Select(t => new PublicTypeBienOption { Id = t.Id, Name = t.Name, Image = t.Image })
                .ToListAsync(ct))
            .OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        foreach (var t in typeBiens) t.Image = PublicCatalogueProjection.FirstImage(t.Image);

        return new PublicFilterOptions
        {
            Projects = projects,
            Quartiers = quartiers,
            Cities = cities,
            PropertyTypes = typeBiens.Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            TypeBiens = typeBiens
        };
    }
}
