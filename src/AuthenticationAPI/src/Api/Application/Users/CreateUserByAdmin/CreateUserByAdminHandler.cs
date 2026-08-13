using AuthenticationAPI.Domain.ApplicationUser.Entities;
using AuthenticationAPI.Infrastructure.Clients;
using Microsoft.AspNetCore.Identity;

namespace AuthenticationAPI.Api.Application.Users.CreateUserByAdmin;

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
    private readonly IProjectApiClient _projectApiClient;

    public CreateUserByAdminHandler(UserManager<User> userManager, RoleManager<Role> roleManager, IProjectApiClient projectApiClient)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _projectApiClient = projectApiClient;
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

        var user = new User
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

        if (!await _roleManager.RoleExistsAsync(roleCode))
        {
            await _roleManager.CreateAsync(new Role { Name = roleCode, DisplayName = roleCode });
        }

        var addToRole = await _userManager.AddToRoleAsync(user, roleCode);
        if (!addToRole.Succeeded)
        {
            var failures = addToRole.Errors
                .Select(e => new ValidationFailure(e.Code, e.Description));
            throw new Common.Exceptions.ValidationException(failures);
        }

        // ProjectAPI keeps its OWN AspNetUsers table for local FKs
        // (Appointment.SalesAgentId, ProjectMembership.UserId, etc.) — an
        // account that only exists here shows up in role pickers but fails
        // with "User not found" the moment ProjectAPI tries to reference it.
        // Only roles ProjectAPI's TPH model actually maps matter there
        // (SALES_AGENT, NOTARY); other roles still get a plain mirrored row
        // so future FKs against them don't hit the same gap.
        await _projectApiClient.ProvisionUserAsync(new ProvisionUserRequest(
            user.Id, user.Email!, user.FirstName, user.LastName, user.PhoneNumber, roleCode), cancellationToken);

        return new CreateUserByAdminResponse
        {
            UserId = user.Id,
            Email = user.Email!,
            Role = roleCode
        };
    }
}
