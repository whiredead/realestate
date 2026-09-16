using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Domain.FeedBacks.Entities;
using ProjectAPI.Domain.FeedBacks.Interfaces;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;
using System.Linq.Expressions;

namespace ProjectAPI.Api.Application.Feedbacks.GetFeedback;

/// <summary>
/// Handler for processing feedback retrieval queries.
/// </summary>
public class GetFeedbackHandler : IRequestHandler<GetFeedbackQuery, PaginatedResponse<FeedbackDetailsResponse>>
{
    private readonly IFeedbackRepository _feedbackRepository;
    private readonly ApplicationDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetFeedbackHandler"/> class.
    /// </summary>
    /// <param name="feedbackRepository">The repository used to retrieve feedback data.</param>
    /// <param name="db">Used to resolve the AgentId filter against ProjectMembership — see remarks on Handle.</param>
    public GetFeedbackHandler(IFeedbackRepository feedbackRepository, ApplicationDbContext db)
    {
        _feedbackRepository = feedbackRepository;
        _db = db;
    }

    /// <summary>
    /// Handles the request to retrieve feedback based on the provided query filters.
    ///
    /// Phase 1 — the AgentId filter used to walk Project.Assignments (the
    /// legacy ProjectAssignments table), which the Phase 1 rewrite of
    /// ProjectAssignmentController no longer writes to at all: any staffing
    /// change made through that controller since Phase 1 would have been
    /// invisible here. Resolved against ProjectMembership instead, which is
    /// the only source of truth for "which projects is this agent on."
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

        HashSet<Guid>? agentProjectIds = null;
        if (request.AgentId.HasValue)
        {
            var agentIdString = request.AgentId.Value.ToString();
            agentProjectIds = (await _db.Set<ProjectMembership>()
                .Where(m => m.UserId == agentIdString && m.RoleCode == RoleCodes.SalesAgent && m.IsActive)
                .Select(m => m.ProjectId)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet();
        }

        // Build a dynamic predicate based on the query filters
        Expression<Func<Feedback, bool>> predicate = f =>
            (string.IsNullOrEmpty(request.UserId) || f.UserId == request.UserId) &&
            (!request.ProjectId.HasValue || f.ProjectId == request.ProjectId) &&
            (agentProjectIds == null || (f.ProjectId.HasValue && agentProjectIds.Contains(f.ProjectId.Value)));

        // Retrieve feedback that matches the filters
        var feedback = (await _feedbackRepository.Find(predicate, includes)).ToList();

        // Paginate and map feedback to the response model
        var totalItems = feedback.Count;
        var paginatedData = feedback
            // Stable order before paging: without it page contents are
            // nondeterministic and rows repeat or vanish between pages.
            .OrderByDescending(f => f.CreatedAt).ThenBy(f => f.Id)
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
                    MinPrice = 0,
                    MaxPrice = 0
                } : null,
                Rating = p.Rating,
                Comments = p.Comments,
                Attachments = p.Attachments,
                CreatedAt = p.CreatedAt
            });

        return new PaginatedResponse<FeedbackDetailsResponse>(paginatedData, request.PageNumber, request.PageSize, totalItems);
    }
}
