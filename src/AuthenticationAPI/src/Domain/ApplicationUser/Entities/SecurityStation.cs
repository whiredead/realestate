namespace AuthenticationAPI.Domain.ApplicationUser.Entities;

/// <summary>
/// Represents an SecurityStation entity
/// </summary>
public class SecurityStation : UserAssignmentBase
{
    /// <summary>
    /// Gets or sets the collection of associated SecurityStation entities.
    /// </summary>
    public ICollection<SecurityStationBch> SecurityStationBchs { get; set; } = [];
}
