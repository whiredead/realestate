namespace ProjectAPI.Api.Application.Identity.Users.ResetPassword;

/// <summary>
/// Completes a forgot-password reset. Requires the token issued by
/// <c>POST /api/User/forgot-password</c> — this endpoint stays
/// [AllowAnonymous] (the caller has no session yet), but the token is what
/// proves they actually received the reset link for this account, not just
/// that they know/guessed a UserId.
/// </summary>
public class ResetPasswordCommand : IRequest<Unit>
{
    public required string UserId { get; init; }
    public required string Token { get; init; }
    public required string NewPassword { get; init; }
    public required string ConfirmPassword { get; set; }

}
