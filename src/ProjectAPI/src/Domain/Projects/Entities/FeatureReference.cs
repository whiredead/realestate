namespace ProjectAPI.Domain.Projects.Entities;

public class FeatureReference
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public string Scope { get; set; } = "BOTH";
    public bool IsActive { get; set; } = true;
}
