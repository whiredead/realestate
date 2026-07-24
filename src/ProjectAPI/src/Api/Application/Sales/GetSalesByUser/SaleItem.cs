namespace ProjectAPI.Api.Application.Sales.GetSalesByUser;

public class SaleItem
{
    public Guid SaleId { get; set; }
    public Guid UnitId { get; set; }
    public DateTime SaleDate { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal Paid { get; set; }
    public decimal Remaining { get; set; }
}