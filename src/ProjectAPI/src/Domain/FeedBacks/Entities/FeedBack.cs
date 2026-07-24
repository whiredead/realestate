using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Domain.FeedBacks.Entities;

/// <summary>
/// Represents feedback provided by a user, which may pertain to a project or the application as a whole.
/// </summary>
public class Feedback
{
    /// <summary>
    /// Gets or sets the unique identifier of the feedback.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the user who provided the feedback.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the project associated with the feedback.
    /// If null, the feedback pertains to the application instead of a specific project.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the rating given by the user, typically on a scale from 1 to 5.
    /// </summary>
    public int Rating { get; set; }

    /// <summary>
    /// Gets or sets the textual comments provided by the user.
    /// </summary>
    public string Comments { get; set; }

    /// <summary>
    /// Gets or sets the list of attachment file URLs associated with the feedback.
    /// </summary>
    public List<string> Attachments { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the timestamp indicating when the feedback was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the user who provided the feedback.
    /// </summary>
    public User User { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the project associated with the feedback.
    /// </summary>
    public Immeuble FeedBack_Project { get; set; }
}
