namespace ProjectAPI.Api.Application.Quartiers.CreateQuartier;

/// <summary>
/// Command for creating a new quartier (district/area).
/// </summary>
public class CreateQuartierCommand : IRequest<CreateQuartierResponse>
{
    /// <summary>
    /// Gets or sets the name of the quartier.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a text description of the quartier.
    /// </summary>
    public string Description { get; set; }

    /// <summary>City (ville) of the quartier. Optional.</summary>
    public string? City { get; set; }

    /// <summary>
    /// Gets or sets the images (or links) for the quartier, e.g., JSON or comma-separated URLs.
    /// </summary>
    public string Images { get; set; }
}
