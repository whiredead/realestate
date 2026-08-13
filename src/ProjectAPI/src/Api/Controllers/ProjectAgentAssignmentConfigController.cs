using Microsoft.AspNetCore.Authorization;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.ProjectAgentAssignmentConfig.GetProjectAgentAssignmentConfig;
using ProjectAPI.Api.Application.ProjectAgentAssignmentConfig.SetProjectAgentAssignmentConfig;

namespace ProjectAPI.Api.Controllers;

/// <summary>
/// A project's sales-agent auto-assignment strategy (round-robin, lowest
/// workload, or a fixed primary agent) — admin-only configuration, read by
/// SalesAgentAssignmentService when a new appointment needs an agent.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/agent-assignment-config")]
[Authorize(Roles = RoleGroups.Admins)]
public class ProjectAgentAssignmentConfigController : ControllerBase
{
    private readonly IMediator _mediator;
    public ProjectAgentAssignmentConfigController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId)
    {
        var res = await _mediator.Send(new GetProjectAgentAssignmentConfigQuery { ProjectId = projectId });
        return res == null ? NotFound() : Ok(res);
    }

    [HttpPut]
    public async Task<IActionResult> Set(Guid projectId, [FromBody] SetProjectAgentAssignmentConfigCommand body)
    {
        body.ProjectId = projectId;
        body.UpdatedByUserId = User.FindFirst("UserId")?.Value;
        var res = await _mediator.Send(body);
        return Ok(res);
    }
}
