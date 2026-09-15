using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Domain.Identity.Entities;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Api.Application.Identity.Users.GetAllUsers;

/// <summary>
/// Represents a response containing details of all users.
/// </summary>
public class GetAllUsersResponse
{
    /// <summary>
    /// Gets or sets the user details.
    /// </summary>
    public UserResponse User { get; set; } = default!;

    /// <summary>
    /// Gets or sets the roles associated with the user.
    /// </summary>
    public IEnumerable<Role> Roles { get; set; } = default!;
}
