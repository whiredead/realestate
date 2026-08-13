using AuthenticationAPI.Domain.ApplicationUser.Entities;
using Microsoft.AspNetCore.Identity;

namespace AuthenticationAPI.Api.Application.Internal.GetUserRoles;

public class GetUserRolesHandler : IRequestHandler<GetUserRolesQuery, GetUserRolesResponse>
{
    private readonly UserManager<User> _userManager;

    public GetUserRolesHandler(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task<GetUserRolesResponse> Handle(GetUserRolesQuery request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            return new GetUserRolesResponse { Exists = false, Roles = Array.Empty<string>() };
        }

        var roles = RoleCodes.Normalize(await _userManager.GetRolesAsync(user));

        return new GetUserRolesResponse
        {
            Exists = true,
            UserId = user.Id,
            Email = user.Email,
            Roles = roles
        };
    }
}
