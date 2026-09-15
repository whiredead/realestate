namespace ProjectAPI.Api.Application.Identity.Users.DeleteUser;

public class DeleteUserCommand : IRequest<DeleteUserResponse>
{
    public required string UserId { get; set; }
}
