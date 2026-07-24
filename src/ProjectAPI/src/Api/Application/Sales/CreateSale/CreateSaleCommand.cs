namespace ProjectAPI.Domain.Sales.CreateSale;

public class CreateSaleCommand : IRequest<CreateSaleResponse>
{
    // Path A: existing user
    public string? BuyerId { get; set; }

    // Path B: manual buyer
    public string? BuyerFirstName { get; set; }
    public string? BuyerLastName { get; set; }
    public string? BuyerEmail { get; set; }
    public string? BuyerPhoneNumber { get; set; }
    public string? BuyerCIN { get; set; }

    public Guid UnitId { get; set; }
    public DateTime SaleDate { get; set; } = DateTime.UtcNow;
    public decimal TotalPrice { get; set; }
    public bool IsUnderConstruction { get; set; }

    public decimal? InitialPaymentAmount { get; set; }
    public Guid? ReservationId { get; set; }
}
