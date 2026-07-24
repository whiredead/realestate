using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Domain.Projects.Entities;

public class LikedProject
{
    public Guid Id { get; set; }
    public string UserId { get; set; } // The ID of the user who liked the project
    public Guid ProjectId { get; set; } // The ID of the liked project
    public DateTime LikedAt { get; set; } // The date and time the project was liked

    public Project Project { get; set; } // Navigation property to the project
    public User User { get; set; }
}