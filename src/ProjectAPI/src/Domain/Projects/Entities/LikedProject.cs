namespace ProjectAPI.Domain.Projects.Entities;

public class LikedProject
{
    public Guid Id { get; set; }

    /// <summary>
    /// Either a real AspNetUsers.Id (signed-in visitor) or a stable anonymous
    /// id generated client-side and carried in a cookie (signed-out visitor,
    /// see gpia_guest_id / GuestFavourite). No FK to Users on purpose — an
    /// anonymous id never has a matching row there, and the point of this
    /// column is "whose favourite is this", not "which account".
    /// </summary>
    public string UserId { get; set; } // The ID of the user (or guest) who liked the project
    public Guid ProjectId { get; set; } // The ID of the liked project
    public DateTime LikedAt { get; set; } // The date and time the project was liked

    public Project Project { get; set; } // Navigation property to the project
}
