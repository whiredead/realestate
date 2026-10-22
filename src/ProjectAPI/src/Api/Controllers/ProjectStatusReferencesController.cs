using Microsoft.AspNetCore.Authorization;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.References.ProjectStatusReferences;

namespace ProjectAPI.Api.Controllers;

[ApiController]
[Route("api/project-status-references")]
public sealed class ProjectStatusReferencesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProjectStatusReferencesController(IMediator mediator) => _mediator = mediator;

    /// <summary>Project creation reads its status choices from this reference table.</summary>
    [HttpGet]
    [Authorize(Roles = RoleGroups.Admins)]
    public async Task<ActionResult<IReadOnlyList<ProjectStatusReferenceDto>>> List(CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetProjectStatusReferencesQuery(), cancellationToken));

    [HttpPost]
    [Authorize(Roles = RoleGroups.GlobalOnly)]
    public async Task<ActionResult<ProjectStatusReferenceDto>> Create(
        [FromBody] CreateProjectStatusReferenceCommand command,
        CancellationToken cancellationToken) =>
        CreatedAtAction(nameof(List), await _mediator.Send(command, cancellationToken));

    /// <summary>Updates presentation and availability only; lifecycle codes are intentionally immutable.</summary>
    [HttpPut("{code}")]
    [Authorize(Roles = RoleGroups.Admins)]
    public async Task<ActionResult<ProjectStatusReferenceDto>> Update(
        string code,
        [FromBody] UpdateProjectStatusReferenceCommand command,
        CancellationToken cancellationToken)
    {
        command.Code = code;
        return Ok(await _mediator.Send(command, cancellationToken));
    }
}
