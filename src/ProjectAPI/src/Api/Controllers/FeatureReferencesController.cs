using Microsoft.AspNetCore.Authorization;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.References.FeatureReferences;
using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Api.Controllers;

[ApiController]
[Route("api/feature-references")]
[Authorize(Roles = RoleGroups.Admins)]
public sealed class FeatureReferencesController : ControllerBase
{
    private readonly IMediator _mediator;
    public FeatureReferencesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public Task<List<FeatureReference>> List(CancellationToken ct) =>
        _mediator.Send(new GetFeatureReferencesQuery(), ct);

    [HttpPost]
    public async Task<ActionResult<FeatureReference>> Create(CreateFeatureReferenceCommand command, CancellationToken ct) =>
        Ok(await _mediator.Send(command, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FeatureReference>> Update(Guid id, UpdateFeatureReferenceCommand command, CancellationToken ct)
    {
        command.Id = id;
        return Ok(await _mediator.Send(command, ct));
    }

    /// <summary>Where an atout is used. Projects and quartiers hold a copy (no foreign key), so usage is matched by name.</summary>
    [HttpGet("{id:guid}/usage")]
    public async Task<ActionResult<FeatureReferenceUsage>> Usage(Guid id, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetFeatureReferenceUsageQuery { Id = id }, ct));

    /// <summary>
    /// Deletes the atout from the referential only. Projects and quartiers that already use it keep
    /// their own copy, so nothing on a public page changes; the UI lists them and asks first.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteFeatureReferenceCommand { Id = id }, ct);
        return NoContent();
    }
}
