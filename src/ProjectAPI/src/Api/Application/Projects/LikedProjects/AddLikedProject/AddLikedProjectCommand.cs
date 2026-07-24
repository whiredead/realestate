namespace ProjectAPI.Api.Application.Projects.LikedProjects.AddLikedProject;

public class AddLikedProjectCommand : IRequest<LikedProjectResponse>
{
    public string UserId { get; set; }
    public Guid ProjectId { get; set; }
}
