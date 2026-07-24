using ProjectAPI.Api.Application.Common.Models;

namespace ProjectAPI.Api.Application.Feedbacks.GetFeedback;

/// <summary>
/// Represents a query to retrieve feedback based on user ID, project ID, or agent ID with optional pagination.
/// </summary>
public class GetFeedbackQuery : IRequest<PaginatedResponse<FeedbackDetailsResponse>>
{
    /// <summary>
    /// Gets or sets the optional user ID to filter feedback by the user who provided it.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the optional project ID to filter feedback for a specific project.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the optional agent ID to filter feedback for projects associated with the agent.
    /// </summary>
    public Guid? AgentId { get; set; }

    /// <summary>
    /// Gets or sets the page number for pagination. Defaults to 1.
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of feedback items per page for pagination. Defaults to 10.
    /// </summary>
    public int PageSize { get; set; } = 10;
}
