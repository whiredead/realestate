namespace ProjectAPI.Api.Application.Sales.ScheduleDelivery;

public class ScheduleDeliveryCommand : IRequest<Guid>
{
    public Guid SaleId { get; set; }
    public Guid UnitId { get; set; }
    public DateTime DeliveryDate { get; set; }
    public string Status { get; set; } = "Scheduled";
    public string Report { get; set; } = "Delivery scheduled";
}