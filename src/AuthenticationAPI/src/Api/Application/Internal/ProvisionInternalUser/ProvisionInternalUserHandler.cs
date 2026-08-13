using AuthenticationAPI.Domain.ApplicationUser.Entities;
using Microsoft.AspNetCore.Identity;

namespace AuthenticationAPI.Api.Application.Internal.ProvisionInternalUser;

/// <summary>
/// Phase 2 — the single place besides RegisterHandler allowed to call
/// CreateAsync/AddToRolesAsync. Reachable only through the internal
/// API-key-protected endpoint (see InternalController), never from a public
/// route, and never [AllowAnonymous].
///
/// Existing-account handling (required by the invitation spec): if an
/// account with this email already exists — e.g. a PROSPECT/BUYER accepting
/// an internal-role invitation, or someone who already holds another
/// internal role — this ADDS the requested role rather than creating a
/// second account, and never touches the existing password. Only the
/// create-new-account branch uses request.Password.
/// </summary>
public class ProvisionInternalUserHandler : IRequestHandler<ProvisionInternalUserCommand, ProvisionInternalUserResponse>
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;

    public ProvisionInternalUserHandler(UserManager<User> userManager, RoleManager<Role> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<ProvisionInternalUserResponse> Handle(ProvisionInternalUserCommand request, CancellationToken cancellationToken)
    {
        var roleCode = RoleCodes.Normalize(request.RoleCode);

        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        var wasExistingAccount = existingUser != null;

        var user = existingUser;
        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid().ToString(),
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                FirstNameAr = string.Empty,
                LastNameAr = string.Empty,
                PhoneNumber = request.PhoneNumber,
                Discriminator = roleCode
            };

            var creation = await _userManager.CreateAsync(user, request.Password);
            if (!creation.Succeeded)
            {
                var failures = creation.Errors
                    .Select(e => new ValidationFailure(e.Code, e.Description));
                throw new Common.Exceptions.ValidationException(failures);
            }
        }

        if (!await _roleManager.RoleExistsAsync(roleCode))
        {
            await _roleManager.CreateAsync(new Role { Name = roleCode, DisplayName = roleCode });
        }

        var currentRoles = RoleCodes.Normalize(await _userManager.GetRolesAsync(user));
        if (!currentRoles.Contains(roleCode, StringComparer.Ordinal))
        {
            var addToRole = await _userManager.AddToRoleAsync(user, roleCode);
            if (!addToRole.Succeeded)
            {
                var failures = addToRole.Errors
                    .Select(e => new ValidationFailure(e.Code, e.Description));
                throw new Common.Exceptions.ValidationException(failures);
            }
        }

        var rolesAfter = RoleCodes.Normalize(await _userManager.GetRolesAsync(user));

        return new ProvisionInternalUserResponse
        {
            UserId = user.Id,
            Email = user.Email!,
            WasExistingAccount = wasExistingAccount,
            RolesAfter = rolesAfter
        };
    }
}
