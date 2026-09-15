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
    /// Gets or sets the floor/level the unit belongs to.
    /// </summary>
    public Guid FloorId { get; set; }
    public Floor Floor { get; set; } = null!;

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
    /// Photo URLs for this specific unit (comma-delimited, same convention as
    /// <see cref="Immeuble.Images"/>). Distinct from the building's own
    /// exterior shots and from <see cref="ImmeublePlanInterieur"/>'s grouped
    /// plan/interior reference photos — this is the unit's own gallery.
    /// </summary>
    public string? Images { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the related project.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Navigation property to the related project.
    /// </summary>
    public Immeuble Immeuble { get; set; }

    /// <summary>
    /// The référentiel layout this unit realises (§7.2) — "Studio",
    /// "Appartement 3 Chambres", "Plateau de Bureaux".
    ///
    /// Nullable on purpose. Until this column existed the catalogue inferred a
    /// unit's type from its bedroom count alone, which cannot separate two
    /// types that share one (a 3-bedroom apartment and a 3-bedroom penthouse,
    /// or a studio and an office plateau at zero). Rows created before the
    /// column, and stock whose layout genuinely is not in the référentiel, stay
    /// null and fall back to that bedroom match — see GetPublicPlansQuery.
    /// </summary>
    public int? TypeBienId { get; set; }

    /// <summary>Navigation to <see cref="TypeBienId"/>.</summary>
    public TypeBien? TypeBien { get; set; }

    /// <summary>
    /// Navigation property for related property deliveries.
    /// </summary>
    public ICollection<PropertyDelivery> PropertyDeliveries { get; set; } = new List<PropertyDelivery>();
    public ICollection<UnitTracking> UnitTrackings { get; set; }= new List<UnitTracking>();

    /// <summary>
    /// Commercial status of the unit (spec §3).
    ///
    /// Do NOT assign this directly. Every change goes through
    /// <c>UnitStatusService</c>, which validates the move against
    /// <see cref="UnitStateMachine"/> and appends a <see cref="UnitStatusHistory"/>
    /// row in the same transaction (§7 forbids direct status updates). The setter
    /// stays public only because EF Core materialises entities through it.
    /// </summary>
    public UnitCommercialStatus Status { get; set; } = UnitCommercialStatus.Available;

    /// <summary>Append-only status trail (§7).</summary>
    public ICollection<UnitStatusHistory> StatusHistory { get; set; } = new List<UnitStatusHistory>();
}
