namespace ProjectAPI.Api.Application.Projects.LikedProjects.UpdateLikedProject;

public class UpdateLikedProjectCommand : IRequest<LikedProjectResponse>
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; } // Allow updating the project
}