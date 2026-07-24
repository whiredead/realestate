namespace AuthenticationAPI.Domain.ApplicationUser.Entities;

/// <summary>
/// Represents the association between an SecurityStation and a Bch.
/// </summary>
public class SecurityStationBch
{
    /// <summary>
    /// Gets or sets the ID of the associated SecurityStation.
    /// </summary>
    public short SecurityStationId { get; set; }

    /// <summary>
    /// Gets or sets the associated SecurityStation entity.
    /// </summary>
    public SecurityStation SecurityStation { get; set; } = default!;

    /// <summary>
    /// Gets or sets the ID of the associated Bch.
    /// </summary>
    public short BchId { get; set; }

    /// <summary>
    /// Gets or sets the associated Bch entity.
    /// </summary>
    public Bch Bch { get; set; } = default!;
}
