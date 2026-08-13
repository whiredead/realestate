namespace AuthenticationAPI.Api.Application.Users.CreateUserByAdmin;

/// <summary>
/// Direct account creation by an administrator — GLOBAL_ADMIN only (see
/// UserController). Unlike RegisterHandler (public, PROSPECT-only,
/// [AllowAnonymous]), this can create an account holding ANY role, including
/// internal ones, with a password the admin sets on the invitee's behalf.
///
/// This is a deliberate second creation path alongside the Phase 2 invitation
/// flow, not a replacement for it: the invitation flow lets the invitee set
/// their own password and accept terms, which this endpoint does not — an
/// admin using this endpoint is vouching for the account directly.
/// </summary>
public class CreateUserByAdminCommand : IRequest<CreateUserByAdminResponse>
{
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Password { get; init; }
    public required string PhoneNumber { get; init; }

    /// <summary>Canonical §6.1 code or legacy label — normalized before use. Exactly one role per account, matching how the rest of the platform models Discriminator.</summary>
    public required string Role { get; init; }
}
