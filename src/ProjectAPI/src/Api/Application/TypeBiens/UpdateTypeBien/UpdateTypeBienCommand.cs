namespace ProjectAPI.Api.Application.TypeBiens.UpdateTypeBien;

/// <summary>
/// Command to update a TypeBien entity.
/// </summary>
public class UpdateTypeBienCommand : IRequest<UpdateTypeBienResponse>
{
    /// <summary>
    /// Gets or sets the unique identifier of the TypeBien to be updated.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the TypeBien (e.g., "Appartement", "Maison", "Villa").
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the optional description of this TypeBien.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the optional link or URL to an image representing the TypeBien.
    /// </summary>
    public string? Image { get; set; }

    /// <summary>
    /// Gets or sets the price of the TypeBien.
    /// </summary>
    public double? Price { get; set; }

    /// <summary>
    /// Gets or sets the number of bedrooms.
    /// </summary>
    public int? NbrChambre { get; set; }

    /// <summary>
    /// Gets or sets the number of bathrooms.
    /// </summary>
    public int? NbrSalleDeBain { get; set; }

    /// <summary>
    /// Gets or sets the number of shower rooms, counted apart from bathrooms.
    /// </summary>
    public int? NbrDouche { get; set; }

    /// <summary>Gets or sets the parking spaces included with this layout.</summary>
    public int? NbrParking { get; set; }

    /// <summary>
    /// Gets or sets the 3D tour of this layout. Validated by MediaUrlPolicy.
    /// </summary>
    public string? Module3DLink { get; set; }

    /// <summary>
    /// Gets or sets the minimum surface area.
    /// </summary>
    public int? MinSurface { get; set; }

    /// <summary>
    /// Gets or sets the maximum surface area.
    /// </summary>
    public int? MaxSurface { get; set; }

    /// <summary>
    /// Gets or sets the interior images.
    /// </summary>
    public string? ImagesInterieur { get; set; }
}