using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Domain.FeedBacks.Entities;
using ProjectAPI.Domain.FeedBacks.Interfaces;
using System.Linq.Expressions;

namespace ProjectAPI.Api.Application.Feedbacks.GetFeedbackById;

/// <summary>
/// Handler for retrieving feedback details along with user and project info.
/// </summary>
public class GetFeedbackByIdHandler : IRequestHandler<GetFeedbackByIdQuery, FeedbackDetailsResponse>
{
    private readonly IFeedbackRepository _feedbackRepository;
    public GetFeedbackByIdHandler(IFeedbackRepository feedbackRepository)
    {
        _feedbackRepository = feedbackRepository;
    }

    public async Task<FeedbackDetailsResponse> Handle(GetFeedbackByIdQuery request, CancellationToken cancellationToken)
    {
        var includes = new Expression<Func<Feedback, object>>[]
            {
                f => f.User,
                f => f.FeedBack_Project
            };

        var feedback = (await _feedbackRepository.Find(f => f.Id == request.FeedbackId, includes)).FirstOrDefault();

        return feedback == null
            ? throw new NotFoundException("Feedback not found.")
            : new FeedbackDetailsResponse
        {
            FeedbackId = feedback.Id,
            User = new UserDto
            {
                Id = feedback.User.Id,
                UserName = feedback.User.UserName!,
                Email = feedback.User.Email!
            },
            Project = feedback.FeedBack_Project != null ? new ImmeubleResponse
            {
                Id = feedback.FeedBack_Project.Id,
                Name = feedback.FeedBack_Project.Name,
                Location = feedback.FeedBack_Project.Location,
                Type =feedback.FeedBack_Project.Type ?? string.Empty,
                MinPrice = feedback.FeedBack_Project.MinPrice,
                MaxPrice = feedback.FeedBack_Project.MaxPrice
            } : null,
            Rating = feedback.Rating,
            Comments = feedback.Comments,
            Attachments = feedback.Attachments,
            CreatedAt = feedback.CreatedAt
        };
    }
}
