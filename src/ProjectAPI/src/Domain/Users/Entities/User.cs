using Microsoft.AspNetCore.Identity;
using ProjectAPI.Domain.Identity.Entities;
using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Domain.Users.Entities;

/// <summary>
/// The single account entity for the whole application.
///
/// This is the TPH root: <see cref="Agent"/> and <see cref="Notary"/> are
/// discriminated subclasses (see ApplicationDbContext.HasDiscriminator), so the
/// CLR type chosen at creation time IS the stored discriminator and a plain
/// User row can never be "upgraded" into an Agent later without a migration.
///
/// Until the APIs were merged there were two of these — one per service, each
/// mapping its own AspNetUsers table in its own database, kept in step by an
/// HTTP provisioning call. There is now one table, so identity fields
/// (FirstNameAr/LastNameAr, the UserRoles navigation) that used to live only on
/// the authentication side are merged in here.
///
/// About/Rating deliberately do NOT live here: they are agent-specific and stay
/// on <see cref="Agent"/>, which is what the existing AspNetUsers columns are
/// already mapped from.
/// </summary>
public class User : IdentityUser
{
    public string FirstName { get; set; }
    public string LastName { get; set; }

    /// <summary>
    /// Arabic given name. Optional, and empty rather than null when unknown:
    /// the column is NOT NULL, so a default of `default!` would make every
    /// caller that does not set it fail on insert.
    /// </summary>
    public string FirstNameAr { get; set; } = string.Empty;

    /// <summary>Arabic family name. Optional — see <see cref="FirstNameAr"/>.</summary>
    public string LastNameAr { get; set; } = string.Empty;

    /// <summary>
    /// Explicit join navigation to roles. Identity's own UserManager APIs remain
    /// the way to ASK whether a user holds a role; this exists so a query can
    /// eager-load roles in one round trip (see <see cref="GetRoleNames"/>).
    /// </summary>
    public ICollection<UserRole> UserRoles { get; set; } = [];

    /// <summary>
    /// Role names from the loaded <see cref="UserRoles"/> graph. Returns empty
    /// when the navigation was not included — it is not a database query, so a
    /// caller that needs authoritative roles must use UserManager instead.
    /// </summary>
    public IList<string> GetRoleNames() =>
        UserRoles.Count != 0 ? UserRoles.Select(ur => ur.Role.Name!).ToList() : [];
}
