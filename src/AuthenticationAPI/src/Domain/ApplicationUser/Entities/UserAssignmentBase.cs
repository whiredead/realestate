namespace AuthenticationAPI.Domain.ApplicationUser.Entities;

/// <summary>
/// Base class representing a user assignment.
/// </summary>
public abstract class UserAssignmentBase
{
    /// <summary>
    /// Gets or sets the unique identifier for the user assignment.
    /// </summary>
    public short Id { get; set; }

    /// <summary>
    /// Gets or sets the Arabic name of the user assignment.
    /// </summary>
    public string NameAr { get; set; } = default!;

    /// <summary>
    /// Gets or sets the French name of the user assignment.
    /// </summary>
    public string NameFr { get; set; } = default!;
}
