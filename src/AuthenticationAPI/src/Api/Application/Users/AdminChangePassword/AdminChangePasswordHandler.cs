using AuthenticationAPI.Api.Application.Common.Exceptions;
using AuthenticationAPI.Domain.ApplicationUser.Entities;
using Microsoft.AspNetCore.Identity;

namespace AuthenticationAPI.Api.Application.Users.AdminChangePassword;

public class AdminChangePasswordHandler : IRequestHandler<AdminChangePasswordCommand, Unit>
{
    private readonly UserManager<User> _userManager;

    public AdminChangePasswordHandler(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task<Unit> Handle(AdminChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId)
            ?? throw new NotFoundException("User Not found!!");

        // RemovePasswordAsync fails harmlessly if the account somehow has no
        // password yet — AddPasswordAsync right after is what actually needs
        // to succeed, so only that result is checked.
        await _userManager.RemovePasswordAsync(user);

        var result = await _userManager.AddPasswordAsync(user, request.NewPassword);
        if (!result.Succeeded)
        {
            var failures = result.Errors
                .Select(e => new ValidationFailure(e.Code, e.Description));
            throw new Common.Exceptions.ValidationException(failures);
        }

        return Unit.Value;
    }
}
