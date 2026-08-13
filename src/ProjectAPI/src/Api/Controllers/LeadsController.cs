using Microsoft.AspNetCore.Authorization;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Leads.GetLeads;

namespace ProjectAPI.Api.Controllers;

/// <summary>
/// Lead (a project favorited by a user) has existed with a real repository
/// and DI wiring since AddLikedProjectHandler started writing rows, but had
/// no controller anywhere — nothing could ever read them back. §6.3 CRM
/// scope: agent/admin, same as appointments/reservations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "Bearer")]
[Authorize(Roles = RoleGroups.AdminsAgents)]
public class LeadsController : ControllerBase
{
    private readonly IMediator _mediator;

    public LeadsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLeads([FromQuery] GetLeadsQuery query)
    {
        var response = await _mediator.Send(query);
        return Ok(response);
    }
}
