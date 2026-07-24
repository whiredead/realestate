using ProjectAPI.Domain.Immeubles.Entities;

namespace ProjectAPI.Domain.Sales.Entities;

/// <summary>
/// Represents the delivery details of a purchased property unit.
/// </summary>
public class PropertyDelivery
{
    /// <summary>
    /// Gets or sets the unique identifier for the property delivery.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the related sale.
    /// </summary>
    public Guid SaleId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the related unit.
    /// </summary>
    public Guid UnitId { get; set; }

    /// <summary>
    /// Gets or sets the date when the property is scheduled to be delivered.
    /// </summary>
    public DateTime DeliveryDate { get; set; }

    /// <summary>
    /// Gets or sets the current status of the property delivery (e.g., Scheduled, Delivered, InProgress).
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the report related to the property delivery, if any.
    /// </summary>
    public string Report { get; set; }

    /// <summary>
    /// Navigation property to the related sale.
    /// </summary>
    public Sale Sale { get; set; }

    /// <summary>
    /// Navigation property to the related unit.
    /// </summary>
    public Unit Unit { get; set; }
}
