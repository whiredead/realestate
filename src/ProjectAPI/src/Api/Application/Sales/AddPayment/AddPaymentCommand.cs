namespace ProjectAPI.Api.Application.Sales.AddPayment;

public class AddPaymentCommand : IRequest<bool>
{
    public Guid SaleId { get; set; }
    public decimal AmountPaid { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Completed";
}