using ProjectAPI.Domain.Identity.Entities;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Domain.Identity.Interfaces;

/// <summary>
/// Provides user-specific repository operations for <see cref="User"/> entities.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Asynchronously retrieves a user by their username, including their roles.
    /// </summary>
    /// <param name="userName">The username of the user to retrieve.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the user if found; otherwise, null.</returns>
    Task<User?> GetUserByName(string userName);

    /// <summary>
    /// Asynchronously retrieves a list of users based on their role.
    /// </summary>
    /// <param name="roleId">The role name to filter users by.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an <see cref="IEnumerable{T}"/> of <see cref="User"/> objects.</returns>
    Task<IEnumerable<User>> GetUsersByRole(string roleId);
}
