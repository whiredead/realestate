namespace ProjectAPI.Api.Application.Common.Models;

/// <summary>
/// Response representing detailed information about a unit within a project.
/// </summary>
public class UnitResponse
{
    /// <summary>
    /// Gets or sets the unique identifier of the unit.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the floor or level of the unit (e.g., "RDC", "1er").
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
    /// Gets or sets the surface area of the apartment in square meters.
    /// </summary>
    public double? ApartmentSurface { get; set; }

    /// <summary>
    /// Gets or sets the surface area of the balcony in square meters.
    /// </summary>
    public double? BalconySurface { get; set; }

    /// <summary>
    /// Gets or sets the surface area of the terrace in square meters.
    /// </summary>
    public double? TerraceSurface { get; set; }

    /// <summary>
    /// Gets or sets the surface area of the private garden in square meters.
    /// </summary>
    public double? GardenSurface { get; set; }

    /// <summary>
    /// Gets or sets the view from the unit (e.g., "Jardin Piscine").
    /// </summary>
    public string View { get; set; }

    /// <summary>
    /// Gets or sets the orientation of the unit (e.g., "Ouest").
    /// </summary>
    public string Orientation { get; set; }

    /// <summary>
    /// Gets or sets the total surface area of the unit (including balcony, terrace, and garden).
    /// </summary>
    public double? TotalSurface { get; set; }

    /// <summary>
    /// Gets or sets the saleable value of the unit using the first formula (50% balcony, 50% terrace, 30% garden).
    /// </summary>
    public double? SaleableValue { get; set; }

    /// <summary>
    /// Gets or sets the saleable value of the unit using the second formula (100% balcony, 50% terrace, 30% garden).
    /// </summary>
    public double? SaleableValue1 { get; set; }

    /// <summary>
    /// Gets or sets the price of the unit based on SaleableValue.
    /// </summary>
    public decimal? PriceSaleableValue { get; set; }

    /// <summary>
    /// Gets or sets the price of the unit based on SaleableValue1.
    /// </summary>
    public decimal? PriceSaleableValue1 { get; set; }

    /// <summary>
    /// Gets or sets the latest price of the unit as of the most recent update.
    /// </summary>
    public decimal? LatestPrice { get; set; }
}
