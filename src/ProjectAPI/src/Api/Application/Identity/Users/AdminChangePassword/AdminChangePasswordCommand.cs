namespace ProjectAPI.Api.Application.Identity.Users.AdminChangePassword;

/// <summary>
/// GLOBAL_ADMIN sets a new password on an existing account directly — no
/// reset token needed, since the caller's own authenticated GLOBAL_ADMIN
/// session (enforced by UserController's [Authorize(Roles = Admins)]) is
/// the authorization, not proof of owning the target account. Distinct from
/// ResetPasswordCommand (the forgot-password flow, token-based, for the
/// account owner acting on their own behalf).
/// </summary>
public class AdminChangePasswordCommand : IRequest<Unit>
{
    public required string UserId { get; init; }
    public required string NewPassword { get; init; }
}
