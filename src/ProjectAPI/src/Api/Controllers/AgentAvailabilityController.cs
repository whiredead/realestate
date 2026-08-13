using Microsoft.AspNetCore.Authorization;
using ProjectAPI.Api.Application.Agents.CreateAgentBlock;
using ProjectAPI.Api.Application.Agents.DeleteAgentBlock;
using ProjectAPI.Api.Application.Agents.DeleteAgentDateOverride;
using ProjectAPI.Api.Application.Agents.GetAgentAvailableSlots;
using ProjectAPI.Api.Application.Agents.GetAgentBlocks;
using ProjectAPI.Api.Application.Agents.GetAgentWeeklyAvailability;
using ProjectAPI.Api.Application.Agents.SetAgentAppointmentSettings;
using ProjectAPI.Api.Application.Agents.SetAgentDateOverride;
using ProjectAPI.Api.Application.Agents.SetAgentWeeklyAvailability;
using ProjectAPI.Api.Application.Common.Security;

namespace ProjectAPI.Api.Controllers;

/// <summary>
/// Sales-agent availability calendar: recurring weekly hours, ad-hoc blocks
/// (leave/closures), date-specific overrides, and slot-duration/buffer
/// settings — mirrors NotaryBlocksController's shape for the agent side.
/// §6.3 — an agent manages their own availability; admins oversee.
/// </summary>
[ApiController]
[Route("api/agents/{agentId}")]
[Authorize(Roles = RoleGroups.AdminsAgents)]
public class AgentAvailabilityController : ControllerBase
{
    private readonly IMediator _mediator;
    public AgentAvailabilityController(IMediator mediator) => _mediator = mediator;

    [HttpPost("blocks")]
    public async Task<IActionResult> CreateBlock(string agentId, [FromBody] CreateAgentBlockCommand body)
    {
        body.AgentId = agentId;
        var res = await _mediator.Send(body);
        return CreatedAtAction(nameof(GetBlocks), new { agentId, from = res.StartUtc, to = res.EndUtc }, res);
    }

    [HttpDelete("blocks/{blockId:guid}")]
    public async Task<IActionResult> DeleteBlock(string agentId, Guid blockId)
    {
        var ok = await _mediator.Send(new DeleteAgentBlockCommand { AgentId = agentId, BlockId = blockId });
        return ok ? NoContent() : NotFound();
    }

    [HttpGet("blocks")]
    public async Task<IActionResult> GetBlocks(string agentId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var res = await _mediator.Send(new GetAgentBlocksQuery { AgentId = agentId, From = from, To = to });
        return Ok(res);
    }

    [HttpPut("weekly-availability")]
    public async Task<IActionResult> SetWeeklyAvailability(string agentId, [FromBody] SetAgentWeeklyAvailabilityCommand body)
    {
        body.AgentId = agentId;
        var res = await _mediator.Send(body);
        return Ok(res);
    }

    [HttpGet("weekly-availability")]
    public async Task<IActionResult> GetWeeklyAvailability(string agentId)
    {
        var res = await _mediator.Send(new GetAgentWeeklyAvailabilityQuery { AgentId = agentId });
        return Ok(res);
    }

    [HttpPost("date-overrides")]
    public async Task<IActionResult> CreateDateOverride(string agentId, [FromBody] SetAgentDateOverrideCommand body)
    {
        body.AgentId = agentId;
        var res = await _mediator.Send(body);
        return CreatedAtAction(nameof(GetWeeklyAvailability), new { agentId }, res);
    }

    [HttpDelete("date-overrides/{overrideId:guid}")]
    public async Task<IActionResult> DeleteDateOverride(string agentId, Guid overrideId)
    {
        var ok = await _mediator.Send(new DeleteAgentDateOverrideCommand { AgentId = agentId, OverrideId = overrideId });
        return ok ? NoContent() : NotFound();
    }

    [HttpPut("appointment-settings")]
    public async Task<IActionResult> SetAppointmentSettings(string agentId, [FromBody] SetAgentAppointmentSettingsCommand body)
    {
        body.AgentId = agentId;
        var res = await _mediator.Send(body);
        return Ok(res);
    }

    [HttpGet("available-slots")]
    public async Task<IActionResult> GetAvailableSlots(string agentId, [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var res = await _mediator.Send(new GetAgentAvailableSlotsQuery { AgentId = agentId, From = from, To = to });
        return Ok(res);
    }
}
