using ProjectAPI.Api.Application.Common.Models;

namespace ProjectAPI.Api.Application.Feedbacks.GetFeedbackById;

/// <summary>
/// Query for retrieving feedback details along with user and project info.
/// </summary>
public class GetFeedbackByIdQuery : IRequest<FeedbackDetailsResponse>
{
    public Guid FeedbackId { get; set; }

}
