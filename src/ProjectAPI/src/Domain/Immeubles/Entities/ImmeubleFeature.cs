namespace ProjectAPI.Domain.Immeubles.Entities;

/// <summary>
/// Represents a feature associated with an immeuble.
/// </summary>
public class ImmeubleFeature
{
    /// <summary>
    /// Gets or sets the unique identifier of the immeuble feature.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the feature (e.g., Climate Control, Elevator).
    /// </summary>
    public string Name { get; set; }
    public string Icon { get; set; }


    /// <summary>
    /// Gets or sets the identifier of the related immeuble.
    /// </summary>
    public Guid ImmeubleId { get; set; }

    /// <summary>
    /// Navigation property to the related immeuble.
    /// </summary>
    public Immeuble Immeuble { get; set; }
}
