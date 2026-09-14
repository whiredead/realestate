using Microsoft.AspNetCore.Authorization;
using ProjectAPI.Api.Application.PublicCatalogue;

namespace ProjectAPI.Api.Controllers;

/// <summary>
/// §7.2 — the anonymous public catalogue.
///
/// Every action here is [AllowAnonymous] and read-only, and every response is a
/// purpose-built DTO from Application/PublicCatalogue. No domain entity is
/// serialised, so no agent, notary, buyer, reservation, immeuble id, floor id,
/// unit id or stock count can leak by someone adding a navigation property
/// somewhere else in the model.
///
/// The whole controller is a single boundary: if it is on this controller, it
/// is published to the internet without a login. Anything that is not safe
/// under that sentence belongs on ProjectController behind [Authorize].
/// </summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
public class PublicCatalogueController : ControllerBase
{
    private readonly IMediator _mediator;

    public PublicCatalogueController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// The plan catalogue — one row per TypeBien offered by a project, filtered
    /// and paged server-side.
    ///
    /// Replaces the browser loading every project, every building, and then one
    /// units request per building before filtering in JavaScript.
    /// </summary>
    [HttpGet("catalogue/plans")]
    [ProducesResponseType(typeof(PublicPlanPage), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlans([FromQuery] GetPublicPlansQuery query, CancellationToken ct)
    {
        return Ok(await _mediator.Send(query, ct));
    }

    /// <summary>
    /// Option lists for the catalogue filters — projects, quartiers and
    /// property types, and nothing else.
    ///
    /// Exists so no public page has to call GET /api/Projects, whose internal
    /// projection hands an anonymous caller the assigned agents and notaries
    /// with their e-mail addresses and phone numbers.
    /// </summary>
    [HttpGet("catalogue/filters")]
    [ProducesResponseType(typeof(PublicFilterOptions), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFilters(CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetPublicFiltersQuery(), ct));
    }

    /// <summary>
    /// One plan, for the public plan-detail page. A commercial layout, never a
    /// unit record.
    /// </summary>
    [HttpGet("catalogue/plans/{projectId:guid}/{typeBienId:int}")]
    [ProducesResponseType(typeof(PublicPlanDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlan(Guid projectId, int typeBienId, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetPublicPlanQuery
        {
            ProjectId = projectId,
            TypeBienId = typeBienId
        }, ct));
    }

    /// <summary>
    /// One project for the public detail page, with its amenities, quartier,
    /// media and plans in a single round trip.
    ///
    /// A project still in DRAFT answers 404, not 403 — to a visitor an
    /// unpublished project does not exist, and "forbidden" would confirm it does.
    /// </summary>
    [HttpGet("projects/{projectId:guid}")]
    [ProducesResponseType(typeof(PublicProjectDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProject(Guid projectId, CancellationToken ct)
    {
        return Ok(await _mediator.Send(new GetPublicProjectQuery { ProjectId = projectId }, ct));
    }
}
