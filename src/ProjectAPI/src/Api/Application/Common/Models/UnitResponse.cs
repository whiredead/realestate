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
    /// Gets or sets the id of the floor the unit belongs to.
    /// </summary>
    public Guid FloorId { get; set; }

    /// <summary>
    /// Gets or sets the floor's display name (e.g., "RDC", "1er").
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

    /// <summary>
    /// §3 commercial status (AVAILABLE, HOLD_PENDING_APPROVAL, RESERVED, …).
    ///
    /// Without it every consumer sees `status: undefined` and cannot tell a free
    /// unit from a sold one — the reservation screen filters on AVAILABLE and so
    /// reported "no unit available" against a stock of 32 free units.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>This unit's own photo URLs (comma-delimited), distinct from the building's Images.</summary>
    public string? Images { get; set; }

    /// <summary>
    /// The référentiel layout this unit realises (§7.2), null when the unit
    /// declares none — see Unit.TypeBienId.
    /// </summary>
    public int? TypeBienId { get; set; }

    /// <summary>
    /// Display name of <see cref="TypeBienId"/> ("Studio Vue Marina"), so a
    /// caller listing units does not need a second request per row to label
    /// them. Null whenever TypeBienId is.
    /// </summary>
    public string? TypeBienName { get; set; }
}
