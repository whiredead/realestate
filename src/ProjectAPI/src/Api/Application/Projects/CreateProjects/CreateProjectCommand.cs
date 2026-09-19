namespace ProjectAPI.Api.Application.Projects.CreateProjects;

public class CreateProjectCommand : IRequest<CreateProjectResponse>
{
    public string Name { get; set; }
    public string Location { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
    public string? Module3DLink { get; set; }
    public List<string> Images { get; set; } = new();

    public string? Type { get; set; }

    /// <summary>Commercial status (§7.5): ComingSoon | UnderConstruction | Available | Sold.</summary>
    public string? StatusGlobal { get; set; }
    public string? StatusReferenceCode { get; set; }

    /// <summary>
    /// §8 — warranty granted on every unit of this project, in months. Omitted
    /// means the entity default (12), the figure the handover used to hard-code.
    /// A sale copies this at creation and freezes it, so editing it later never
    /// changes a warranty already granted.
    /// </summary>
    public int? WarrantyMonths { get; set; }

    // Optional: existing Quartier
    public Guid? QuartierId { get; set; }

    /// <summary>Property types offered by the project. Null leaves the links unchanged (update); the list replaces them.</summary>
    public List<int>? TypeBienIds { get; set; }

    // Or create new Quartier inline
    public string? QuartierName { get; set; }
    public string? QuartierDescription { get; set; }
    public string? QuartierImages { get; set; }
}
