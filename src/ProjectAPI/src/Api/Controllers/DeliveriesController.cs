using ProjectAPI.Api.Application.Sales.ScheduleDelivery;
using ProjectAPI.Api.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;

namespace ProjectAPI.Api.Controllers;

[ApiController]
[Route("api/deliveries")]
[Authorize(Roles = RoleGroups.AdminsAgents)] // §6.3 Livraison: "C/M affecté" agent, "A périmètre" admin.
public class DeliveriesController : ControllerBase
{
    private readonly IMediator _mediator;
    public DeliveriesController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public Task<Guid> Schedule(ScheduleDeliveryCommand cmd) => _mediator.Send(cmd);

    [HttpPatch("{deliveryId:guid}/status")]
    public Task<bool> UpdateStatus(Guid deliveryId, [FromBody] UpdateDeliveryStatusCommand cmd)
    {
        cmd.DeliveryId = deliveryId;
        return _mediator.Send(cmd);
    }
}
