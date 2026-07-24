using ProjectAPI.Domain.Immeubles.Entities;

namespace ProjectAPI.Domain.Sales.Entities;
/// <summary>
/// Represents a sale made by an agent to a user.
/// </summary>
public class Sale
{
    public Guid Id { get; set; }

    // Optional user in your system
    public string? BuyerId { get; set; }

    // Required snapshot (always filled either from BuyerId or manual entry)
    public string BuyerFirstName { get; set; } = null!;
    public string BuyerLastName { get; set; } = null!;
    public string BuyerEmail { get; set; } = null!;
    public string BuyerPhoneNumber { get; set; } = null!;
    public string? BuyerCIN { get; set; }

    public Guid UnitId { get; set; }
    public Unit Unit { get; set; }
    public DateTime SaleDate { get; set; }
    public decimal TotalPrice { get; set; }
    public bool IsUnderConstruction { get; set; }

    public ICollection<PaymentTracking> Payments { get; set; } = [];
    public ICollection<PropertyDelivery> PropertyDeliveries { get; set; } = [];
}
