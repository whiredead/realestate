using Microsoft.AspNetCore.Identity;

namespace ProjectAPI.Domain.Identity.Entities;

/// <summary>
/// An application role. The canonical role vocabulary itself lives in
/// <see cref="Users.Entities.RoleCodes"/>; this is only the stored row.
///
/// Richer than a plain IdentityRole because AspNetRoles carries DisplayName
/// (NOT NULL) and RoleAr. The merged application therefore registers Identity
/// with this type rather than IdentityRole — using IdentityRole would fail to
/// map DisplayName and break inserts against the existing table.
/// </summary>
public class Role : IdentityRole<string>
{
    /// <summary>Role name in Arabic.</summary>
    public string? RoleAr { get; set; } = default!;

    /// <summary>
    /// Human-readable label. NOT NULL in the database, so it defaults to empty
    /// rather than `default!` — a role created without one must not fail on
    /// insert. Callers that have nothing better set it to the role code.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Join navigation to the users holding this role.</summary>
    public ICollection<UserRole> UserRoles { get; set; } = [];
}
