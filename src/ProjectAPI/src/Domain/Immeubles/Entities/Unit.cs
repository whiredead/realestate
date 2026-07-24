using ProjectAPI.Domain.Sales.Entities;

namespace ProjectAPI.Domain.Immeubles.Entities;

/// <summary>
/// Represents a specific unit within a project.
/// </summary>
public class Unit
{
    /// <summary>
    /// Gets or sets the unique identifier for the unit.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the floor or level of the unit.
    /// </summary>
    public string Floor { get; set; }

    /// <summary>
    /// Gets or sets the unit number.
    /// </summary>
    public string UnitNumber { get; set; }

    /// <summary>
    /// Gets or sets the number of bedrooms in the unit.
    /// </summary>
    public int? NumberOfBedrooms { get; set; }

    /// <summary>
    /// Gets or sets the number of bathrooms in the unit.
    /// </summary>
    public int? NumberOfBathrooms { get; set; }

    /// <summary>
    /// Gets or sets the surface area of the apartment.
    /// </summary>
    public double? ApartmentSurface { get; set; }

    /// <summary>
    /// Gets or sets the balcony surface area.
    /// </summary>
    public double? BalconySurface { get; set; }

    /// <summary>
    /// Gets or sets the terrace surface area.
    /// </summary>
    public double? TerraceSurface { get; set; }

    /// <summary>
    /// Gets or sets the garden surface area.
    /// </summary>
    public double? GardenSurface { get; set; }

    /// <summary>
    /// Gets or sets the view from the unit.
    /// </summary>
    public string View { get; set; }

    /// <summary>
    /// Gets or sets the orientation of the unit.
    /// </summary>
    public string Orientation { get; set; }

    /// <summary>
    /// Gets or sets the total surface area of the unit.
    /// </summary>
    public double? TotalSurface { get; set; }

    /// <summary>
    /// Gets or sets the saleable value of the unit using the first formula.
    /// </summary>
    public double? SaleableValue { get; set; }

    /// <summary>
    /// Gets or sets the saleable value of the unit using the second formula.
    /// </summary>
    public double? SaleableValue1 { get; set; }

    /// <summary>
    /// Gets or sets the price calculated using SaleableValue.
    /// </summary>
    public decimal? PriceSaleableValue { get; set; }

    /// <summary>
    /// Gets or sets the price calculated using SaleableValue1.
    /// </summary>
    public decimal? PriceSaleableValue1 { get; set; }

    /// <summary>
    /// Gets or sets the price as of the latest date.
    /// </summary>
    public decimal? LatestPrice { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the related project.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Navigation property to the related project.
    /// </summary>
    public Immeuble Immeuble { get; set; }

    /// <summary>
    /// Navigation property for related property deliveries.
    /// </summary>
    public ICollection<PropertyDelivery> PropertyDeliveries { get; set; } = new List<PropertyDelivery>();
    public ICollection<UnitTracking> UnitTrackings { get; set; }= new List<UnitTracking>();

    /// <summary>
    /// Gets or sets the status of the unit.
    /// </summary>
    public string Status { get; set; } = "Available"; // e.g., "Available", "Sold"
}
