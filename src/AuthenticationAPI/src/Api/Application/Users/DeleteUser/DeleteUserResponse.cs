namespace AuthenticationAPI.Api.Application.Users.DeleteUser;

public class DeleteUserResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Dependencies { get; set; } = new();
}
