using ProjectAPI.Api.Application.Construction.AddMilestone;
using ProjectAPI.Api.Application.Construction.CompleteProject;
using ProjectAPI.Api.Application.Construction.FinalizeProject;
using ProjectAPI.Api.Application.Construction.GetProjectConstruction;
using ProjectAPI.Api.Application.Construction.GetTitleStatus;
using ProjectAPI.Api.Application.Construction.PublishConstructionUpdate;
using ProjectAPI.Api.Application.Construction.UpdateMilestoneStatus;
using ProjectAPI.Api.Application.Construction.UpdateTitleStatus;
using ProjectAPI.Api.Application.Construction.ValidateMilestone;
using ProjectAPI.Api.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;

namespace ProjectAPI.Api.Controllers;

/// <summary>
/// Construction tracking (spec §15) and land-title status (§16).
/// </summary>
[ApiController]
[Route("api/construction")]
[Authorize] // Published progress is public (opt-out below); writes are admin (§6.3).
public class ConstructionController : ControllerBase
{
    private readonly IMediator _mediator;

    public ConstructionController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Milestones and published updates for a project (§15).</summary>
    [HttpGet("projects/{projectId:guid}")]
    [AllowAnonymous] // §6.3 Avancement construction: "L publié" pour le visiteur.
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProjectConstruction(Guid projectId, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetProjectConstructionQuery { ProjectId = projectId }, ct));
    }

    /// <summary>Land-title status and history for a unit (§16).</summary>
    [HttpGet("units/{unitId:guid}/title")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTitle(Guid unitId, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetTitleStatusQuery { UnitId = unitId }, ct));
    }

    /// <summary>Moves the title status, tracing the change (§16).</summary>
    [Authorize(Roles = RoleGroups.Admins)] // §16 title status — admin-managed.
    [HttpPatch("units/{unitId:guid}/title")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateTitle(Guid unitId, [FromBody] UpdateTitleStatusCommand body)
    {
        body.UnitId = unitId;
        return Ok(await _mediator.Send(body));
    }

    /// <summary>Creates a weighted milestone (§15.1).</summary>
    [Authorize(Roles = RoleGroups.Admins)] // §15 planning — admin.
    [HttpPost("projects/{projectId:guid}/milestones")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddMilestone(Guid projectId, [FromBody] AddMilestoneCommand body)
    {
        body.ProjectId = projectId;
        return Ok(await _mediator.Send(body));
    }

    /// <summary>Updates a milestone's status (§15.1 FR-CON-002).</summary>
    [Authorize(Roles = RoleGroups.Admins)]
    [HttpPatch("milestones/{milestoneId:guid}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMilestoneStatus(
        Guid milestoneId,
        [FromBody] UpdateMilestoneStatusCommand body)
    {
        body.MilestoneId = milestoneId;
        return Ok(await _mediator.Send(body));
    }

    [Authorize(Roles = RoleGroups.Admins)]
    [HttpPost("milestones/{milestoneId:guid}/validate")]
    public async Task<IActionResult> ValidateMilestone(Guid milestoneId)
        => Ok(await _mediator.Send(new ValidateMilestoneCommand { MilestoneId = milestoneId }));

    /// <summary>Publishes a progress update (§15.2).</summary>
    [Authorize(Roles = RoleGroups.Admins)] // §15.2 FR-CON-003 publication — admin.
    [HttpPost("projects/{projectId:guid}/updates")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PublishUpdate(Guid projectId, [FromBody] PublishConstructionUpdateCommand body)
    {
        body.ProjectId = projectId;
        return Ok(await _mediator.Send(body));
    }

    /// <summary>Marks the project COMPLETED, opening final visits (§15.3, §17.1).</summary>
    [Authorize(Roles = RoleGroups.Admins)] // §15.3 FR-CON-005 — admin.
    [HttpPost("projects/{projectId:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CompleteProject(Guid projectId, [FromBody] CompleteProjectCommand body)
    {
        body.ProjectId = projectId;
        return Ok(await _mediator.Send(body));
    }

    /// <summary>EN_LIVRAISON → FINALISE: closes the project, which becomes read-only.</summary>
    [Authorize(Roles = RoleGroups.Admins)]
    [HttpPost("projects/{projectId:guid}/finalize")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> FinalizeProject(Guid projectId, [FromBody] FinalizeProjectCommand body)
    {
        body.ProjectId = projectId;
        return Ok(await _mediator.Send(body));
    }
}
