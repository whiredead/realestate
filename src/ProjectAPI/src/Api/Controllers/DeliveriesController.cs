using ProjectAPI.Api.Application.Sales.ScheduleDelivery;

namespace ProjectAPI.Api.Controllers;

[ApiController]
[Route("api/deliveries")]
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
