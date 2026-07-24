using ProjectAPI.Api.Application.Notary.CreateNotaryBlocks;
using ProjectAPI.Api.Application.Notary.DeleteNotaryBlock;
using ProjectAPI.Api.Application.Notary.GetNotaryBlocks;

namespace ProjectAPI.Api.Controllers;
[ApiController]
[Route("api/notaries/{notaryId}/blocks")]
public class NotaryBlocksController : ControllerBase
{
    private readonly IMediator _mediator;
    public NotaryBlocksController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateNotaryBlockCommand body)
    {
        var res = await _mediator.Send(body);
        return CreatedAtAction(nameof(GetRange), new { body.NotaryId, from = res.StartUtc, to = res.EndUtc }, res);
    }

    [HttpDelete("{blockId:guid}")]
    public async Task<IActionResult> Delete(string notaryId, Guid blockId)
    {
        var ok = await _mediator.Send(new DeleteNotaryBlockCommand { NotaryId = notaryId, BlockId = blockId });
        return ok ? NoContent() : NotFound();
    }

    [HttpGet]
    public async Task<IActionResult> GetRange(string notaryId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var res = await _mediator.Send(new GetNotaryBlocksQuery { NotaryId = notaryId, From = from, To = to });
        return Ok(res);
    }
}