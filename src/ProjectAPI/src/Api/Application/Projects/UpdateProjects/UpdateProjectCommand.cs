namespace ProjectAPI.Api.Application.Projects.UpdateProjects;

public class UpdateProjectCommand : IRequest<ProjectResponse>
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Location { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
    public string? Module3DLink { get; set; }
    public List<string> Images { get; set; } = new();
    public string? Type { get; set; }
    public string? StatusGlobal { get; set; }
    public decimal? OverallProgress { get; set; }

    /// <summary>
    /// §8 — warranty length in months. Null leaves it unchanged. Changing it
    /// affects sales opened FROM NOW ON only: every existing sale carries its
    /// own frozen copy.
    /// </summary>
    public int? WarrantyMonths { get; set; }

    // Optional: update existing Quartier or create new
    public Guid? QuartierId { get; set; }
    public string? QuartierName { get; set; }
    public string? QuartierDescription { get; set; }
    public string? QuartierImages { get; set; }
}
