namespace ProjectAPI.Api.Application.Projects.LikedProjects.RemoveLikedProject;

public class RemoveLikedProjectCommand : IRequest<RemoveLikedProjectResponse>
{
    /// <summary>
    /// The user who is un-liking.
    /// </summary>
    public string UserId { get; set; } = default!;

    /// <summary>
    /// The project being un-liked.
    /// </summary>
    public Guid ProjectId { get; set; }
}
