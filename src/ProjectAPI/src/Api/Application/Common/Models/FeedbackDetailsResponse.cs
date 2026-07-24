namespace ProjectAPI.Api.Application.Common.Models;

/// <summary>
/// Represents the feedback details response, including user and project information.
/// </summary>
public class FeedbackDetailsResponse
{
    public Guid FeedbackId { get; set; }
    public UserDto User { get; set; }
    public ImmeubleResponse? Project { get; set; }
    public int Rating { get; set; }
    public string Comments { get; set; }
    public List<string> Attachments { get; set; }
    public DateTime CreatedAt { get; set; }
}
