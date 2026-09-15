using ProjectAPI.Domain.Identity.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace ProjectAPI.Api.Application.Identity.Users.ForgotPassword;

/// <summary>
/// Issues a real Identity password-reset token and emails it — the
/// legitimate counterpart to ResetPasswordHandler's fix (which now requires
/// this token instead of accepting a bare UserId from anyone).
///
/// Deliberately silent on "no such account": returning success either way
/// stops this endpoint from being usable to enumerate which emails have
/// accounts (the same reasoning BusinessRuleException.ProjectScopeDenied
/// gives for not naming the project — do not confirm/deny existence).
/// </summary>
public class ForgotPasswordHandler : IRequestHandler<ForgotPasswordCommand, Unit>
{
    private readonly UserManager<User> _userManager;
    private readonly IEmailService _emailService;

    public ForgotPasswordHandler(UserManager<User> userManager, IEmailService emailService)
    {
        _userManager = userManager;
        _emailService = emailService;
    }

    public async Task<Unit> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return Unit.Value;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        await _emailService.SendEmailAsync(
            request.Email,
            "Réinitialisation de votre mot de passe",
            $"Utilisez ce code pour réinitialiser votre mot de passe : {token}");

        return Unit.Value;
    }
}
