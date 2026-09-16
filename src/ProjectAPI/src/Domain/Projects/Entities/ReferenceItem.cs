namespace ProjectAPI.Domain.Projects.Entities;

/// <summary>Generic SaaS reference item. Category + code are unique and stable.</summary>
public class ReferenceItem
{
    public Guid Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
