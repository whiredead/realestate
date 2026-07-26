namespace ProjectAPI.Domain.Projects.Entities;

/// <summary>
/// A neighbourhood-level amenity shown on a project's "Quartier" tab — the
/// surrounding area's assets (schools, transit, shops, security…), distinct
/// from the building's own features (<see cref="ProjectFeature"/>).
///
/// Scoped to the project (like <see cref="ProjectFeature"/>) rather than the
/// Quartier entity, because quartier data is denormalised onto the project.
/// </summary>
public class QuartierAmenity
{
    public Guid Id { get; set; }

    /// <summary>Amenity label, e.g. "Écoles" / "Transports".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Icon glyph (emoji or short token), rendered next to the name.</summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>The project this neighbourhood amenity is shown for.</summary>
    public Guid ProjectId { get; set; }
}
