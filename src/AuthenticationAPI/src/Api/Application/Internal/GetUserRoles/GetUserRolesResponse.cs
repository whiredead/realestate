namespace AuthenticationAPI.Api.Application.Internal.GetUserRoles;

public class GetUserRolesResponse
{
    public required bool Exists { get; init; }
    public string? UserId { get; init; }
    public string? Email { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
}
