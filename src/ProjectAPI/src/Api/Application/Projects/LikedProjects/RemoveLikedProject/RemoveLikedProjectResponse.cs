namespace ProjectAPI.Api.Application.Projects.LikedProjects.RemoveLikedProject;

public class RemoveLikedProjectResponse
{
    /// <summary>
    /// Whether the unlike succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// A human‐readable message.
    /// </summary>
    public string Message { get; set; } = default!;
}
