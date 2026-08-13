using ProjectAPI.Api.Application.Notary.CreateNotaryBlocks;
using ProjectAPI.Api.Application.Notary.DeleteNotaryBlock;
using ProjectAPI.Api.Application.Notary.GetNotaryBlocks;
using ProjectAPI.Api.Application.Notary.GetNotaryWeeklyAvailability;
using ProjectAPI.Api.Application.Notary.SetNotaryWeeklyAvailability;
using ProjectAPI.Api.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;

namespace ProjectAPI.Api.Controllers;
[ApiController]
[Route("api/notaries/{notaryId}")]
[Authorize(Roles = RoleGroups.AdminsNotary)] // §6.3 — a notary manages their own availability; admins oversee.
public class NotaryBlocksController : ControllerBase
{
    private readonly IMediator _mediator;
    public NotaryBlocksController(IMediator mediator) => _mediator = mediator;

    [HttpPost("blocks")]
    public async Task<IActionResult> Create([FromBody] CreateNotaryBlockCommand body)
    {
        var res = await _mediator.Send(body);
        return CreatedAtAction(nameof(GetRange), new { body.NotaryId, from = res.StartUtc, to = res.EndUtc }, res);
    }

    [HttpDelete("blocks/{blockId:guid}")]
    public async Task<IActionResult> Delete(string notaryId, Guid blockId)
    {
        var ok = await _mediator.Send(new DeleteNotaryBlockCommand { NotaryId = notaryId, BlockId = blockId });
        return ok ? NoContent() : NotFound();
    }

    [HttpGet("blocks")]
    public async Task<IActionResult> GetRange(string notaryId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var res = await _mediator.Send(new GetNotaryBlocksQuery { NotaryId = notaryId, From = from, To = to });
        return Ok(res);
    }

    /// <summary>Was read-only via GetNotaryCalendarHandler internally — no way for a notary/admin to actually set their weekly schedule until this.</summary>
    [HttpPut("weekly-availability")]
    public async Task<IActionResult> SetWeeklyAvailability(string notaryId, [FromBody] SetNotaryWeeklyAvailabilityCommand body)
    {
        body.NotaryId = notaryId;
        var res = await _mediator.Send(body);
        return Ok(res);
    }

    [HttpGet("weekly-availability")]
    public async Task<IActionResult> GetWeeklyAvailability(string notaryId)
    {
        var res = await _mediator.Send(new GetNotaryWeeklyAvailabilityQuery { NotaryId = notaryId });
        return Ok(res);
    }
}