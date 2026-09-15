using Microsoft.AspNetCore.Identity;
using ProjectAPI.Domain.Identity.Entities;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Api.Application.Identity.Users.CreateUserByAdmin;

/// <summary>
/// GLOBAL_ADMIN-only direct account creation — see the command's doc comment
/// for why this exists alongside RegisterHandler (PROSPECT-only, public) and
/// the Phase 2 invitation flow (invitee sets their own password). This is
/// the one path where an admin can create an account for ANY role in a
/// single call, admin-set password included.
/// </summary>
public class CreateUserByAdminHandler : IRequestHandler<CreateUserByAdminCommand, CreateUserByAdminResponse>
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;

    public CreateUserByAdminHandler(UserManager<User> userManager, RoleManager<Role> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<CreateUserByAdminResponse> Handle(CreateUserByAdminCommand request, CancellationToken cancellationToken)
    {
        var roleCode = RoleCodes.Normalize(request.Role);

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null)
        {
            throw new Common.Exceptions.ValidationException(new[]
            {
                new ValidationFailure(nameof(request.Email), "Un compte existe déjà avec cet email.")
            });
        }

        // The role must exist before the account is written: a failure here used
        // to leave a created user with no role at all. Role derives from
        // IdentityRole<string>, whose constructor assigns no Id.
        if (!await _roleManager.RoleExistsAsync(roleCode))
        {
            await _roleManager.CreateAsync(new Role { Id = Guid.NewGuid().ToString(), Name = roleCode, DisplayName = roleCode });
        }

        // TPH: the CLR type chosen here IS the stored discriminator (see
        // ApplicationDbContext.HasDiscriminator), and a base User row cannot
        // be upgraded into an Agent/Notary afterwards. Roles the model maps to
        // a subclass must therefore be instantiated as that subclass — an
        // account created as a plain User for SALES_AGENT would satisfy every
        // role check yet fail to load as an Agent wherever one is required.
        User user = roleCode switch
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

        var addToRole = await _userManager.AddToRoleAsync(user, roleCode);
        if (!addToRole.Succeeded)
        {
            var failures = addToRole.Errors
                .Select(e => new ValidationFailure(e.Code, e.Description));
            throw new Common.Exceptions.ValidationException(failures);
        }

        // No mirroring step: there is one AspNetUsers table, so the row written
        // above is already the one the project-side foreign keys
        // (Appointment.SalesAgentId, ProjectMembership.UserId, …) point at.
        // This used to require an HTTP call into the other service to copy the
        // account across two databases.

        return new CreateUserByAdminResponse
        {
            UserId = user.Id,
            Email = user.Email!,
            Role = roleCode
        };
    }
}
