using Microsoft.AspNetCore.Identity;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Domain.Identity.Entities;

/// <summary>
/// The AspNetUserRoles join row, with navigations so a user's roles can be
/// eager-loaded in one query (see <see cref="User.GetRoleNames"/>) instead of
/// a round trip per user.
/// </summary>
public class UserRole : IdentityUserRole<string>
{
    public User User { get; set; } = default!;

    public Role Role { get; set; } = default!;
}
