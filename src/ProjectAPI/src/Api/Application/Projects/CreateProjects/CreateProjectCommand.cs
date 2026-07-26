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

    // Optional: existing Quartier
    public Guid? QuartierId { get; set; }

    // Or create new Quartier inline
    public string? QuartierName { get; set; }
    public string? QuartierDescription { get; set; }
    public string? QuartierImages { get; set; }
}