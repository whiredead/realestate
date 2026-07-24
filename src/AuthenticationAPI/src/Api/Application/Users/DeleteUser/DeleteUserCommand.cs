namespace AuthenticationAPI.Api.Application.Users.DeleteUser;

public class DeleteUserCommand : IRequest<DeleteUserResponse>
{
    public required string UserId { get; set; }
}
