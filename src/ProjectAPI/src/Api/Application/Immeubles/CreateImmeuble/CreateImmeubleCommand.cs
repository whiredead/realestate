namespace ProjectAPI.Api.Application.Immeubles.CreateImmeuble;

/// <summary>
/// Command to create a new immeuble.
/// </summary>
public class CreateImmeubleCommand : IRequest<Guid>
{
    /// <summary>
    /// Gets or sets the name of the immeuble.
    /// </summary>
    public string Name { get; set; }
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the location of the immeuble.
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// Gets or sets the type of the immeuble (e.g., residential, commercial).
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the minimum price of the immeuble.
    /// </summary>
    public decimal MinPrice { get; set; }

    /// <summary>
    /// Gets or sets the maximum price of the immeuble.
    /// </summary>
    public decimal MaxPrice { get; set; }

    /// <summary>
    /// Gets or sets the images associated with the immeuble.
    /// </summary>
    public string Images { get; set; }

    /// <summary>
    /// Gets or sets the description of the immeuble.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the latitude of the immeuble location.
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Gets or sets the longitude of the immeuble location.
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Gets or sets the status of the immeuble (e.g., ComingSoon, Available).
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the number of units in the immeuble.
    /// </summary>
    public int NumberOfUnits { get; set; }

    /// <summary>
    /// Gets or sets the minimum sellable surface area range of the immeuble.
    /// </summary>
    public int MinSellableSurfaceRange { get; set; }

    /// <summary>
    /// Gets or sets the maximum sellable surface area range of the immeuble.
    /// </summary>
    public int MaxSellableSurfaceRange { get; set; }
    public string? Module3DLink { get; set; }
    public int NumberOfSoldUnites { get; set; }

    /// <summary>
    /// Gets or sets the total number of units still available in the Immeuble.
    /// </summary>
    public int NumberOfAvailableUnites { get; set; }

    /// <summary>
    /// Gets or sets the percentage of units sold in the Immeuble.
    /// </summary>
    public int SellsPercentage { get; set; }
}