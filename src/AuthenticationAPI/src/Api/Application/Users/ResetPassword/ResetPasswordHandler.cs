using AuthenticationAPI.Api.Application.Common.Exceptions;
using AuthenticationAPI.Domain.ApplicationUser.Entities;
using Microsoft.AspNetCore.Identity;

namespace AuthenticationAPI.Api.Application.Users.ResetPassword;

/// <summary>
/// Was previously self-issuing a reset token and consuming it in the same
/// call — [AllowAnonymous] plus a bare UserId meant anyone could reset any
/// account's password with no proof they ever received a reset link. Now
/// requires and validates the token from ForgotPasswordHandler; a wrong or
/// expired token fails the same way ResetPasswordAsync already reports
/// ASP.NET Identity token failures (IdentityResult.Errors), via
/// ValidationException like every other Identity-backed handler here.
/// </summary>
public class ResetPasswordHandler : IRequestHandler<ResetPasswordCommand, Unit>
{
    private readonly UserManager<User> _userManager;

    public ResetPasswordHandler(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task<Unit> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId) ??
            throw new NotFoundException("User Not found!!");

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            var failures = result.Errors
                .Select(e => new ValidationFailure(e.Code, e.Description));
            throw new Common.Exceptions.ValidationException(failures);
        }

        return Unit.Value;
    }
}
