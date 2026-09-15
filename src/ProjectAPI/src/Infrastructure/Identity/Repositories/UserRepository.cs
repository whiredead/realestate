using ProjectAPI.Domain.Identity.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Identity.Interfaces;
using ProjectAPI.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace ProjectAPI.Infrastructure.Identity.Repositories;

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
        // AspNetRoles carries BOTH vocabularies as separate rows (e.g. "Agent"
        // AND "SALES_AGENT" both exist) — a seeded account's AspNetUserRoles
        // link points at whichever row the seeder used (the legacy one, in
        // practice). Matching Role.Name == role verbatim therefore only ever
        // found results for the exact legacy label; a caller passing the spec
        // code (as every other part of this codebase does, per RoleCodes'
        // own doc comment: "Authorisation MUST be expressed in spec codes")
        // got back an empty list. Normalize both sides through RoleCodes so
        // either vocabulary resolves to the same accounts.
        var wantedCode = RoleCodes.Normalize(role);

        var matchingRoleIds = await _context.Roles
            .Where(r => r.Name != null)
            .Select(r => new { r.Id, r.Name })
            .ToListAsync();

        var roleIds = matchingRoleIds
            .Where(r => RoleCodes.Normalize(r.Name) == wantedCode)
            .Select(r => r.Id)
            .ToHashSet();

        if (roleIds.Count == 0)
        {
            return [];
        }

        return await _context.Users
            .Where(u => u.UserRoles.Any(ur => roleIds.Contains(ur.RoleId)))
            .ToListAsync();
    }
}
