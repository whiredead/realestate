namespace ProjectAPI.Api.Application.Units.CreateProjectUnit;

/// <summary>
/// Command to create a new immeuble unit.
/// </summary>
public class CreateProjectUnitCommand : IRequest<CreateProjectUnitResponse>
{
    /// <summary>
    /// Gets or sets the immeuble ID to which the unit belongs.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the floor where the unit is located.
    /// </summary>
    public string Floor { get; set; }

    /// <summary>
    /// Gets or sets the unit number.
    /// </summary>
    public string UnitNumber { get; set; }

    /// <summary>
    /// Gets or sets the number of bedrooms in the unit.
    /// </summary>
    public int NumberOfBedrooms { get; set; }

    /// <summary>
    /// Gets or sets the number of bathrooms in the unit.
    /// </summary>
    public int NumberOfBathrooms { get; set; }

    /// <summary>
    /// Gets or sets the surface area of the apartment in square meters.
    /// </summary>
    public double ApartmentSurface { get; set; }

    /// <summary>
    /// Gets or sets the surface area of the balcony in square meters, if applicable.
    /// </summary>
    public double? BalconySurface { get; set; }

    /// <summary>
    /// Gets or sets the surface area of the terrace in square meters, if applicable.
    /// </summary>
    public double? TerraceSurface { get; set; }

    /// <summary>
    /// Gets or sets the surface area of the garden in square meters, if applicable.
    /// </summary>
    public double? GardenSurface { get; set; }

    /// <summary>
    /// Gets or sets the view description of the unit (e.g., sea view, city view).
    /// </summary>
    public string View { get; set; }

    /// <summary>
    /// Gets or sets the orientation of the unit (e.g., north, south).
    /// </summary>
    public string Orientation { get; set; }

    /// <summary>
    /// Gets or sets the total surface area of the unit in square meters.
    /// </summary>
    public double TotalSurface { get; set; }

    /// <summary>
    /// Gets or sets the price of the unit, if applicable.
    /// </summary>
    public decimal? Price { get; set; }
}