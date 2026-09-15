namespace ProjectAPI.Api.Application.Units.UpdateUnit;

/// <summary>
/// Command to update a unit.
/// </summary>
public class UpdateUnitCommand : IRequest<UpdateUnitResponse>
{
    /// <summary>
    /// Gets or sets the unique identifier of the unit to be updated.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the id of the floor the unit belongs to.
    /// </summary>
    public Guid? FloorId { get; set; }

    /// <summary>
    /// Gets or sets the unit number. Null means "leave unchanged" — the
    /// handler already only writes this when non-null (see updateActions
    /// below); it was non-nullable only on the C# type, which made
    /// [ApiController]'s implicit model validation reject a request that
    /// omitted it, even for a caller (e.g. the price/image-only edit modal)
    /// that never meant to touch it (N29).
    /// </summary>
    public string? UnitNumber { get; set; }

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
    /// Gets or sets the view from the unit. Null means "leave unchanged" — see the UnitNumber note (N29).
    /// </summary>
    public string? View { get; set; }

    /// <summary>
    /// Gets or sets the orientation of the unit. Null means "leave unchanged" — see the UnitNumber note (N29).
    /// </summary>
    public string? Orientation { get; set; }

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
    /// Gets or sets the price calculated using the first saleable value formula.
    /// </summary>
    public decimal? PriceSaleableValue { get; set; }

    /// <summary>
    /// Gets or sets the price calculated using the second saleable value formula.
    /// </summary>
    public decimal? PriceSaleableValue1 { get; set; }

    /// <summary>
    /// Gets or sets the latest price of the unit.
    /// </summary>
    public decimal? LatestPrice { get; set; }

    /// <summary>
    /// Gets or sets this unit's own photo URLs (comma-delimited). Null means
    /// "leave unchanged" (same convention as every other optional field here);
    /// pass an empty string to clear it.
    /// </summary>
    public string? Images { get; set; }

    /// <summary>
    /// The référentiel layout this unit realises (§7.2). Null leaves the
    /// current value alone, like every other field here; clearing a type that
    /// was set is done by passing 0, which the handler maps back to null.
    /// </summary>
    public int? TypeBienId { get; set; }

    // Status is deliberately absent (§7: direct status updates are forbidden).
    // A unit's commercial status is a consequence of the sale workflow — it moves
    // only through the reservation, notary and handover commands, via
    // UnitStatusService, which validates the §3 matrix and writes history.
    // Reinstating a Status field here would reopen the hole that let an
    // administrator hand-edit a unit back to AVAILABLE.
}
