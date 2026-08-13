using ProjectAPI.Api.Application.AdminDashboards.Dashboard;
using ProjectAPI.Api.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;

namespace ProjectAPI.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // F4 — the frontend route (/admin index) and nav entry have always been
    // ADMINS_AGENTS (a SALES_AGENT has their own dashboard tile), but this
    // was Admins-only: a SALES_AGENT could reach the page and got a bare 403
    // on the one call it makes. GetAdminDashboardHandler was already
    // correctly project-scoped for any caller via ProjectScopeService
    // (fixed earlier this session) — the mismatch was only ever this
    // attribute, not the data.
    [Authorize(Roles = RoleGroups.AdminsAgents)] // Admin reporting dashboard (§26.4/§26.5).
    public class AdminDashboardController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AdminDashboardController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("overview")]
        public async Task<IActionResult> GetDashboardOverview([FromQuery] AdminDashboardQuery query)
        {
            var response = await _mediator.Send(query);
            return Ok(response);
        }
    }
}
