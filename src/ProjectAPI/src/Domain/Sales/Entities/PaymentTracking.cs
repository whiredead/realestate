namespace ProjectAPI.Domain.Sales.Entities;
/// <summary>
/// Represents the tracking of payments made by a user.
/// </summary>
public class PaymentTracking
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; } // Reference to the sale
    public decimal AmountPaid { get; set; }
    public DateTime PaymentDate { get; set; }
    public DateTime? NextDueDate { get; set; } // For upcoming payments if applicable
    public string Status { get; set; } // e.g., Completed, Pending
}
