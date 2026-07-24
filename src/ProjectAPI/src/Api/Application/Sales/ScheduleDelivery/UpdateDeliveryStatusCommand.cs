namespace ProjectAPI.Api.Application.Sales.ScheduleDelivery;

public class UpdateDeliveryStatusCommand : IRequest<bool>
{
    public Guid DeliveryId { get; set; }
    public string Status { get; set; }         // e.g., InProgress, Delivered
    public string Report { get; set; }         // update note
}