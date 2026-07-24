namespace ProjectAPI.Api.Application.Sales.GetSalesByUser;

public class UserSalesResponse
{
    public List<SaleItem> Sales { get; set; } = new();
    public decimal TotalPrice { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalRemaining { get; set; }
}