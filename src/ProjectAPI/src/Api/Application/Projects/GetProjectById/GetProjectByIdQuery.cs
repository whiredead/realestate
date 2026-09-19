namespace ProjectAPI.Api.Application.Projects.GetProjectById;

/// <summary>
/// Drill-down entrypoint: a single project plus its immeubles (buildings),
/// each carrying live per-building stats (units total/sold/available, price
/// range, sales velocity) — the "Projet → Immeuble" level of the
/// Projet → Immeuble → Étage → Unité hierarchy. No such single-project route
/// existed before (only the paginated list); this is net new, not a rename.
/// </summary>
public class GetProjectByIdQuery : IRequest<ProjectDrillDownResponse>
{
    public Guid Id { get; set; }
}

public class ProjectDrillDownResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string StatusGlobal { get; set; } = string.Empty;
    public string? StatusReferenceCode { get; set; }
    public decimal OverAllProgress { get; set; }
    public List<string> Images { get; set; } = new();

    /// <summary>Roll-up across every immeuble in this project — the project-level tile.</summary>
    public int TotalUnits { get; set; }
    public int AvailableUnits { get; set; }
    public int ReservedUnits { get; set; }
    public int SoldUnits { get; set; }
    public double SellThroughPct { get; set; }

    public List<ImmeubleDrillDownDto> Immeubles { get; set; } = new();
}

public class ImmeubleDrillDownDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Status { get; set; }
    public string? ImagePrincipale { get; set; }

    public int TotalUnits { get; set; }
    public int AvailableUnits { get; set; }
    public int ReservedUnits { get; set; }
    public int SoldUnits { get; set; }
    public double SellThroughPct { get; set; }

    /// <summary>Units sold in the trailing 90 days, /30 — a stable per-building velocity figure that doesn't need a caller-supplied date range.</summary>
    public double RecentUnitsPerMonth { get; set; }

    public int FloorCount { get; set; }
}
