
using ProjectAPI.Api.Application.Common.Models;

namespace ProjectAPI.Api.Application.Identity.Users.GetUsersByRole;

/// <summary>
/// Represents a query for retrieving users by their role.
/// </summary>
public record GetUsersByRoleQuery : IRequest<List<UserResponse>>
{
    /// <summary>
    /// Gets the role for which users are queried.
    /// </summary>
    public string Role { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GetUsersByRoleQuery"/> class.
    /// </summary>
    /// <param name="role">The role for which users are queried.</param>
    public GetUsersByRoleQuery(string role)
    {
        Role = role;
    }
}
