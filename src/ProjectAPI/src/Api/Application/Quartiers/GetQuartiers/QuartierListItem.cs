namespace ProjectAPI.Api.Application.Quartiers.GetQuartiers;

/// <summary>
/// Represents a single quartier in the list response.
/// </summary>
public class QuartierListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; }
}