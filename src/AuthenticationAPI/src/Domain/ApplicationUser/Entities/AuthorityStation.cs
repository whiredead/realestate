namespace AuthenticationAPI.Domain.ApplicationUser.Entities;

/// <summary>
/// Represents an AuthorityStation entity
/// </summary>
public class AuthorityStation : UserAssignmentBase
{
    /// <summary>
    /// Gets or sets the collection of associated AuthorityStationBch entities.
    /// </summary>
    public ICollection<AuthorityStationBch> AuthorityStationBchs { get; set; } = [];
}
