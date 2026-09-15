namespace ProjectAPI.Api.Application.Identity.Users.CreateUserByAdmin;

public class CreateUserByAdminResponse
{
    public required string UserId { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
}
