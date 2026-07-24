using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Domain.FeedBacks.Entities;
using ProjectAPI.Domain.FeedBacks.Interfaces;
using System.Linq.Expressions;

namespace ProjectAPI.Api.Application.Feedbacks.GetFeedback;

/// <summary>
/// Handler for processing feedback retrieval queries.
/// </summary>
public class GetFeedbackHandler : IRequestHandler<GetFeedbackQuery, PaginatedResponse<FeedbackDetailsResponse>>
{
    private readonly IFeedbackRepository _feedbackRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetFeedbackHandler"/> class.
    /// </summary>
    /// <param name="feedbackRepository">The repository used to retrieve feedback data.</param>
    public GetFeedbackHandler(IFeedbackRepository feedbackRepository)
    {
        _feedbackRepository = feedbackRepository;
    }

    /// <summary>
    /// Handles the request to retrieve feedback based on the provided query filters.
    /// </summary>
    /// <param name="request">The query containing filters for user ID, project ID, and agent ID.</param>
    /// <param name="cancellationToken">A cancellation token for the asynchronous operation.</param>
    /// <returns>A paginated response containing the feedback details that match the filters.</returns>
    public async Task<PaginatedResponse<FeedbackDetailsResponse>> Handle(GetFeedbackQuery request, CancellationToken cancellationToken)
    {
        // Define the relationships to include in the query
        var includes = new Expression<Func<Feedback, object>>[]
        {
            f => f.User,
            f => f.FeedBack_Project
        };

        // Build a dynamic predicate based on the query filters
        Expression<Func<Feedback, bool>> predicate = f =>
            (string.IsNullOrEmpty(request.UserId) || f.UserId == request.UserId) &&
            (!request.ProjectId.HasValue || f.ProjectId == request.ProjectId) &&
            (!request.AgentId.HasValue || f.FeedBack_Project.Assignments.Any(a => a.AgentId == request.AgentId.ToString()));

        // Retrieve feedback that matches the filters
        var feedback = (await _feedbackRepository.Find(predicate, includes)).ToList();

        // Paginate and map feedback to the response model
        var totalItems = feedback.Count;
        var paginatedData = feedback
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new FeedbackDetailsResponse
            {
                FeedbackId = p.Id,
                User = new UserDto
                {
                    Id = p.User.Id,
                    UserName = p.User.UserName!,
                    Email = p.User.Email!
                },
                Project = p.FeedBack_Project != null ? new ImmeubleResponse
                {
                    Id = p.FeedBack_Project.Id,
                    Name = p.FeedBack_Project.Name,
                    Location = p.FeedBack_Project.Location,
                    Type = p.FeedBack_Project.Type ?? string.Empty,
                    MinPrice = p.FeedBack_Project.MinPrice,
                    MaxPrice = p.FeedBack_Project.MaxPrice
                } : null,
                Rating = p.Rating,
                Comments = p.Comments,
                Attachments = p.Attachments,
                CreatedAt = p.CreatedAt
            });

        return new PaginatedResponse<FeedbackDetailsResponse>(paginatedData, request.PageNumber, request.PageSize, totalItems);
    }
}
