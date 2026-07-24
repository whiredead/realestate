namespace AuthenticationAPI.Domain.ApplicationUser.Entities;

/// <summary>
/// Represents the association between an AuthorityStation and a Bch.
/// </summary>
public class AuthorityStationBch
{
    /// <summary>
    /// Gets or sets the ID of the associated AuthorityStation.
    /// </summary>
    public short AuthorityStationId { get; set; }

    /// <summary>
    /// Gets or sets the associated AuthorityStation entity.
    /// </summary>
    public AuthorityStation AuthorityStation { get; set; } = default!;

    /// <summary>
    /// Gets or sets the ID of the associated Bch.
    /// </summary>
    public short BchId { get; set; }

    /// <summary>
    /// Gets or sets the associated Bch entity.
    /// </summary>
    public Bch Bch { get; set; } = default!;
}
