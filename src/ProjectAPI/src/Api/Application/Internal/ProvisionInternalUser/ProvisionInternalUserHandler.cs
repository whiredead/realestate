using Microsoft.AspNetCore.Identity;
using ProjectAPI.Domain.Identity.Entities;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Api.Application.Internal.ProvisionInternalUser;

/// <summary>
/// The single place besides RegisterHandler allowed to call
/// CreateAsync/AddToRolesAsync — the identity module owns every write to
/// AspNetUsers (§6.1). Other modules provision an account by sending this
/// command, never by touching UserManager themselves.
///
/// It used to be reachable only over an API-key-protected HTTP endpoint,
/// because the caller lived in a separate service. Both now run in one
/// process, so callers send the command directly through MediatR and the
/// endpoint, its shared key and the HTTP client are gone.
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
            // TPH: the CLR type chosen HERE is the stored discriminator (see
            // ApplicationDbContext.HasDiscriminator). It cannot be assigned as
            // a plain property and a base User row can never be upgraded into
            // an Agent/Notary later without a migration, so the role has to
            // pick the type at construction time.
            user = roleCode switch
            {
                RoleCodes.SalesAgent => new Agent { About = string.Empty },
                RoleCodes.Notary => new Domain.Users.Entities.Notary(),
                _ => new User(),
            };

            user.Id = Guid.NewGuid().ToString();
            user.UserName = request.Email;
            user.Email = request.Email;
            user.FirstName = request.FirstName;
            user.LastName = request.LastName;
            user.FirstNameAr = string.Empty;
            user.LastNameAr = string.Empty;
            user.PhoneNumber = request.PhoneNumber;

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
            // Role derives from IdentityRole<string>, whose constructor assigns no
            // Id: without one EF refuses to track the entity.
            await _roleManager.CreateAsync(new Role { Id = Guid.NewGuid().ToString(), Name = roleCode, DisplayName = roleCode });
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
