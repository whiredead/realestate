using Microsoft.AspNetCore.Authorization;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.References.ReferenceItems;
using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Api.Controllers;

[ApiController, Route("api/reference-items"), Authorize(Roles = RoleGroups.Admins)]
public sealed class ReferenceItemsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ReferenceItemsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public Task<List<ReferenceItem>> List([FromQuery] string? category, CancellationToken ct) =>
        _mediator.Send(new GetReferenceItemsQuery { Category = category }, ct);

    [HttpPost]
    public async Task<ActionResult<ReferenceItem>> Create(CreateReferenceItemCommand command, CancellationToken ct) =>
        Ok(await _mediator.Send(command, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ReferenceItem>> Update(Guid id, UpdateReferenceItemCommand command, CancellationToken ct)
    {
        command.Id = id;
        return Ok(await _mediator.Send(command, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteReferenceItemCommand { Id = id }, ct);
        return NoContent();
    }
}
