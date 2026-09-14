namespace ProjectAPI.Api.Application.Quartiers.GetQuartiers;

/// <summary>
/// Represents a single quartier in the list response.
/// </summary>
public class QuartierListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; }

    /// <summary>Shown in the referential table; the list used to carry only id and name.</summary>
    public string? Description { get; set; }

    /// <summary>Comma-separated image URLs, as stored.</summary>
    public string? Images { get; set; }
}