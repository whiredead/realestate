using ProjectAPI.Api.Application.AdminDashboards.Dashboard;

namespace ProjectAPI.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
