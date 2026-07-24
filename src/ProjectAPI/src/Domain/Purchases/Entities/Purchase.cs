using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Sales.Entities;

namespace ProjectAPI.Domain.Purchases.Entities;

/// <summary>
/// Represents a purchase (achat) made by a user.
/// </summary>
public class Purchase
{
    /// <summary>
    /// Gets or sets the unique identifier for the purchase.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who made the purchase.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the sale associated with this purchase (if any).
    /// </summary>
    public Guid? SaleId { get; set; }

    /// <summary>
    /// Gets or sets the reservation associated with this purchase (if any).
    /// </summary>
    public Guid? ReservationId { get; set; }

    /// <summary>
    /// Gets or sets the notary appointment associated with this purchase (if any).
    /// </summary>
    public Guid? NotaryAppointmentId { get; set; }

    /// <summary>
    /// Gets or sets the total price of the property.
    /// </summary>
    public decimal TotalPrice { get; set; }

    /// <summary>
    /// Gets or sets the amount already paid by the buyer.
    /// </summary>
    public decimal PaidAmount { get; set; }

    /// <summary>
    /// Gets or sets the remaining amount to be paid.
    /// </summary>
    public decimal RemainingAmount { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the purchase was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
    public Sale? Sale { get; set; }
    public Reservation? Reservation { get; set; }


    /// <summary>
    /// Gets or sets the collection of documents associated with this purchase.
    /// </summary>
    //public ICollection<PurchaseDocument> Documents { get; set; } = new List<PurchaseDocument>();
}
