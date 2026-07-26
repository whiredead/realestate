using Microsoft.AspNetCore.Authorization;
using ProjectAPI.Api.Application.Feedbacks.GetFeedback;
using ProjectAPI.Api.Application.Feedbacks.GetFeedbackById;
using ProjectAPI.Api.Application.Feedbacks.SubmitFeedback;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Extensions;
using System.Security.Claims;

namespace ProjectAPI.Api.Controllers;

/// <summary>
/// Controller for handling feedback submissions on projects or the application.
/// </summary>    

[Authorize(AuthenticationSchemes = "Bearer")]
[ApiController]
[Route("api/[controller]")]
public class FeedbackController : ControllerBase
{
    private readonly IMediator _mediator;

    public FeedbackController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Submits feedback for a specific project or the application.
    /// </summary>
    /// <param name="command">The feedback command containing user ID, project ID, rating, and comments.</param>
    /// <returns>A response indicating the result of the feedback submission.</returns>
    [HttpPost("submit-feedback")]
    //[CustomAuthorize("Acheteur")]
    [AllowAnonymous]
    public async Task<IActionResult> SubmitFeedback([FromBody] SubmitFeedbackCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }
    /// <summary>
    /// Gets feedback by its ID.
    /// </summary>
    /// <param name="feedbackId">The ID of the feedback to retrieve.</param>
    /// <returns>A response containing feedback details, including user and project information.</returns>
    [HttpGet("{feedbackId}")]
    // Authenticated (class-level Bearer). The inline role gate below narrows it.
    public async Task<IActionResult> GetFeedbackById(Guid feedbackId)
    {
        var rolesClaim = User.FindFirst("Roles")?.Value ?? User.FindFirst(ClaimTypes.Role)?.Value;
        if (rolesClaim != null)
        {
            var roles = rolesClaim.Split(',').Select(r => r.Trim());
            if (!roles.Contains("Admin") && !roles.Contains("Agent") && !roles.Contains("Acheteur"))
            {
                return Forbid();
            }
        }
        else
        {
            return Forbid();
        }
        var query = new GetFeedbackByIdQuery { FeedbackId = feedbackId };
        var feedback = await _mediator.Send(query);
        return Ok(feedback);
    }
    /// <summary>
    /// Gets feedback based on optional filters: UserId, ProjectId, or AgentId.
    /// </summary>
    /// <param name="userId">The ID of the user for which feedback is to be retrieved (optional).</param>
    /// <param name="projectId">The ID of the project for which feedback is to be retrieved (optional).</param>
    /// <param name="agentId">The ID of the agent for which feedback is to be retrieved (optional).</param>
    /// <param name="pageNumber">The current page number for pagination (default is 1).</param>
    /// <param name="pageSize">The number of items per page for pagination (default is 10).</param>
    /// <returns>A response containing a paginated list of feedback matching the filters.</returns>
    [HttpGet("feedbacks")]
    [Authorize(Roles = RoleGroups.AdminsAgents)] // §8.6 modération — admin/agent.
    public async Task<IActionResult> GetFeedbacks(
        [FromQuery] string? userId,
        [FromQuery] Guid? projectId,
        [FromQuery] Guid? agentId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = new GetFeedbackQuery
        {
            UserId = userId,
            ProjectId = projectId,
            AgentId = agentId,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var feedbackList = await _mediator.Send(query);
        return Ok(feedbackList);
    }

}
