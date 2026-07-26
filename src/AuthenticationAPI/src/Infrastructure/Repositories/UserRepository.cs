using AuthenticationAPI.Domain.ApplicationUser.Entities;
using AuthenticationAPI.Domain.ApplicationUser.Interfaces;
using AuthenticationAPI.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAPI.Infrastructure.Repositories;

/// <summary>
/// Represents the repository for managing <see cref="User"/> entities.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserRepository"/> class.
    /// </summary>
    /// <param name="context">The database context to be used by this repository.</param>
    /// <exception cref="ArgumentNullException">Thrown when the context is null.</exception>
    public UserRepository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task<User?> GetUserByName(string userName)
    {
        // Accepts a username OR an email address. The sign-in form asks for an
        // email (§6.2 makes the verified email the account's identity, and it is
        // what users actually remember), but this lookup previously matched
        // UserName only — so typing the address the form asked for returned
        // "Invalid username or password".
        //
        // Matching is done on Identity's normalised columns, which are stored
        // upper-cased, so the comparison is case-insensitive regardless of the
        // database collation.
        if (string.IsNullOrWhiteSpace(userName))
        {
            return null;
        }

        var normalized = userName.Trim().ToUpperInvariant();

        return await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u =>
                u.NormalizedUserName == normalized || u.NormalizedEmail == normalized);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<User>> GetUsersByRole(string role)
    {
        var roleEntity = await _context.Roles.FirstOrDefaultAsync(r => r.Name == role);
        if (roleEntity == null)
        {
            return [];
        }

        return await _context.Users
            .Where(u => u.UserRoles.Any(ur => ur.RoleId == roleEntity.Id))
            .ToListAsync();
    }
}
