namespace ProjectAPI.Api.Application.Internal.ProvisionUser;

/// <summary>
/// Mirrors an AuthenticationAPI account into ProjectAPI's own AspNetUsers
/// table — called by AuthenticationAPI's CreateUserByAdminHandler right
/// after it creates the real account, so the SAME Id resolves in both
/// services' FKs (Appointment.SalesAgentId, ProjectMembership.UserId, etc.).
/// Idempotent: if the Id already exists here, this is a no-op success, not
/// an error — a retry or a duplicate call must not fail the admin's request.
/// </summary>
public class ProvisionUserCommand : IRequest<ProvisionUserResponse>
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? PhoneNumber { get; init; }
    /// <summary>Canonical §6.1 code — only SALES_AGENT and NOTARY map to a TPH subtype here; anything else is stored as the base User row.</summary>
    public required string Role { get; init; }
}
