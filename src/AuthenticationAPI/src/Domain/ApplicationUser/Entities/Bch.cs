namespace AuthenticationAPI.Domain.ApplicationUser.Entities;

/// <summary>
/// Represents a Bch entity.
/// </summary>
public class Bch : UserAssignmentBase
{
    /// <summary>
    /// Gets or sets the phone number of the Bch.
    /// </summary>
    public string PhoneNumber { get; set; } = default!;

    /// <summary>
    /// Gets or sets the secondary phone number of the Bch.
    /// </summary>
    public string PhoneNumber2 { get; set; } = default!;

    /// <summary>
    /// Gets or sets the collection of associated AuthorityStationBch entities.
    /// </summary>
    public ICollection<AuthorityStationBch> AuthorityStationBchs { get; set; } = [];

    /// <summary>
    /// Gets or sets the collection of associated SecurityStationBchs entities.
    /// </summary>
    public ICollection<SecurityStationBch> SecurityStationBchs { get; set; } = [];
}
