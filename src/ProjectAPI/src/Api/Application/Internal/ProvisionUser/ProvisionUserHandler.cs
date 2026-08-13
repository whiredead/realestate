using Microsoft.AspNetCore.Identity;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Api.Application.Internal.ProvisionUser;

public class ProvisionUserHandler : IRequestHandler<ProvisionUserCommand, ProvisionUserResponse>
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public ProvisionUserHandler(UserManager<User> userManager, RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<ProvisionUserResponse> Handle(ProvisionUserCommand request, CancellationToken cancellationToken)
    {
        var existing = await _userManager.FindByIdAsync(request.Id);
        if (existing != null)
        {
            // Idempotent AND self-healing: a prior call may have created the
            // row but failed before granting the role (e.g. this exact gap,
            // caught live — CreateProjectMembershipHandler checks
            // UserManager.GetRolesAsync, not just the TPH Discriminator).
            await EnsureRoleAsync(existing, request.Role, cancellationToken);
            return new ProvisionUserResponse { Id = existing.Id, AlreadyExisted = true };
        }

        // TPH: the CLR type at creation time IS the discriminator (see
        // ApplicationDbContext.HasDiscriminator) — Agent/Notary must be
        // instantiated directly, a base User row can never be "upgraded"
        // into one later without a migration.
        User user = request.Role switch
        {
            RoleCodes.SalesAgent => new Agent { About = string.Empty },
            RoleCodes.Notary => new ProjectAPI.Domain.Users.Entities.Notary(),
            _ => new User(),
        };

        user.Id = request.Id;
        user.UserName = request.Email;
        user.Email = request.Email;
        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.PhoneNumber = request.PhoneNumber;
        user.EmailConfirmed = true;

        // This service never authenticates logins directly (JWTs from
        // AuthenticationAPI are validated by shared signing key) — the
        // password is unusable and exists only because Identity requires one.
        var result = await _userManager.CreateAsync(user, Guid.NewGuid().ToString("N") + "Aa1!");
        if (!result.Succeeded)
        {
            var failures = result.Errors.Select(e => new ValidationFailure(e.Code, e.Description));
            throw new Common.Exceptions.ValidationException(failures);
        }

        await EnsureRoleAsync(user, request.Role, cancellationToken);

        return new ProvisionUserResponse { Id = user.Id, AlreadyExisted = false };
    }

    /// <summary>
    /// Handlers here (e.g. CreateProjectMembershipHandler) check
    /// UserManager.GetRolesAsync against THIS database's AspNetUserRoles,
    /// not the TPH Discriminator — a mirrored account with no role row
    /// passes existence checks but fails every "does this user actually
    /// hold role X" check.
    /// </summary>
    private async Task EnsureRoleAsync(User user, string roleCode, CancellationToken ct)
    {
        if (!await _roleManager.RoleExistsAsync(roleCode))
        {
            await _roleManager.CreateAsync(new IdentityRole(roleCode));
        }

        if (!await _userManager.IsInRoleAsync(user, roleCode))
        {
            await _userManager.AddToRoleAsync(user, roleCode);
        }
    }
}
