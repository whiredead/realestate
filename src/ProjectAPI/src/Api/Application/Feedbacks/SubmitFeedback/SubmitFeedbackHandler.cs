using Microsoft.AspNetCore.Identity;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.FeedBacks.Entities;
using ProjectAPI.Domain.FeedBacks.Interfaces;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Api.Application.Feedbacks.SubmitFeedback;

/// <summary>
/// Handler for submitting feedback on a project or the application.
/// </summary>
public class SubmitFeedbackHandler : IRequestHandler<SubmitFeedbackCommand, string>
{
    private readonly IFeedbackRepository _feedbackRepository;
    private readonly IImmeubleRepository _projectRepository;
    private readonly UserManager<User> _userManager;

    public SubmitFeedbackHandler(IFeedbackRepository feedbackRepository, IImmeubleRepository projectRepository, UserManager<User> userManager)
    {
        _feedbackRepository = feedbackRepository;
        _projectRepository = projectRepository;
        _userManager = userManager;
    }

    public async Task<string> Handle(SubmitFeedbackCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(request.UserId) ?? throw new NotFoundException("User not found.");
            Immeuble project = null;
            if (request.ProjectId.HasValue)
            {
                project = await _projectRepository.GetByIDAsync(request.ProjectId.Value);
                if (project == null)
                {
                    throw new NotFoundException("Project not found.");
                }
            }

            var feedback = new Feedback
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                ProjectId = request.ProjectId,
                Rating = request.Rating,
                Comments = request.Comments,
                Attachments = request.Attachements,
                CreatedAt = DateTime.UtcNow
            };

            await _feedbackRepository.InsertAsync(feedback);
            await _feedbackRepository.SaveAsync();

            return "Feedback submitted successfully.";
        }catch(Exception ex)
        {
            return ex.ToString();
        }

    }
}
