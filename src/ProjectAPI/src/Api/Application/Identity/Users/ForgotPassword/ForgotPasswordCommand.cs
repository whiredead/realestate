namespace ProjectAPI.Api.Application.Identity.Users.ForgotPassword;

/// <summary>
/// First half of the forgot-password flow — issues a reset token and emails
/// it. The second half is ResetPasswordCommand, which requires this token.
/// </summary>
public class ForgotPasswordCommand : IRequest<Unit>
{
    public required string Email { get; init; }
}
