namespace ProjectAPI.Domain.Sales.CreateSale;

public class CreateSaleResponse
{
    public Guid SaleId { get; set; }
    public Guid PurchaseId { get; set; }
    public string Message { get; set; } = "";
}
