using Microsoft.AspNetCore.Authorization;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.ProjectMemberships.CreateProjectMembership;
using ProjectAPI.Api.Application.ProjectMemberships.EndProjectMembership;
using ProjectAPI.Api.Application.ProjectMemberships.GetAllProjectMemberships;
using ProjectAPI.Api.Application.ProjectMemberships.GetProjectMembershipById;

namespace ProjectAPI.Api.Controllers;

/// <summary>
/// Phase 1 — the generic replacement for ProjectAssignmentController,
/// supporting SALES_AGENT/TECHNICIAN/NOTARY/PROJECT_ADMIN memberships with a
/// validity window. ProjectAssignmentController stays live as a legacy
/// compatibility shim (see its own doc comment) — do not extend it further;
/// new staffing features belong here.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RoleGroups.Admins)] // §6.3 "Utilisateurs internes" / "rôles et affectations" — admins only.
public class ProjectMembershipController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProjectMembershipController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectMembershipCommand command)
    {
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    /// <summary>Deactivates a membership. The row is retained for audit history — never deleted.</summary>
    [HttpPost("{id}/end")]
    public async Task<IActionResult> End(Guid id)
    {
        var response = await _mediator.Send(new EndProjectMembershipCommand { Id = id });
        if (!response.Success)
            return BadRequest(response.Message);
        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await _mediator.Send(new GetProjectMembershipByIdQuery { Id = id });
        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? projectId,
        [FromQuery] string? userId,
        [FromQuery] string? roleCode,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = new GetAllProjectMembershipsQuery
        {
            ProjectId = projectId,
            UserId = userId,
            RoleCode = roleCode,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var response = await _mediator.Send(query);
        return Ok(response);
    }
}
