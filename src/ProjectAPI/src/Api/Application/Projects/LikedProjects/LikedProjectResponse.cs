namespace ProjectAPI.Api.Application.Projects.LikedProjects;

public class LikedProjectResponse
{
    public Guid Id { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; } // User's username
    public string FirstName { get; set; } // User's first name
    public string LastName { get; set; } // User's last name
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; }
    public string ProjectLocation { get; set; }
    public DateTime LikedAt { get; set; }
    public string Message { get; set; }
}
