using ProjectAPI.Api.Application.AdminDashboards.Dashboard;
using ProjectAPI.Api.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;

namespace ProjectAPI.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = RoleGroups.Admins)] // Admin reporting dashboard (§26.4/§26.5).
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
