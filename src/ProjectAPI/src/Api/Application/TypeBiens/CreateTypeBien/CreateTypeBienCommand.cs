namespace ProjectAPI.Api.Application.TypeBiens.CreateTypeBien
{
    /// <summary>
    /// Command to create a new TypeBien entity.
    /// </summary>
    public class CreateTypeBienCommand : IRequest<CreateTypeBienResponse>
    {
        /// <summary>
        /// Name of the TypeBien (e.g., "Appartement", "Maison", "Villa").
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Optional description of this TypeBien.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Optional link or URL to an image representing the TypeBien.
        /// </summary>
        public string? Image { get; set; }

        public double? Price { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public int? NbrChambre { get; set; }
        public int? NbrSalleDeBain { get; set; }

        /// <summary>Shower rooms, counted apart from bathrooms (§7.2).</summary>
        public int? NbrDouche { get; set; }

        /// <summary>Parking spaces included with this layout.</summary>
        public int? NbrParking { get; set; }

        public int? MinSurface { get; set; }
        public int? MaxSurface { get; set; }
        public string? ImagesInterieur { get; set; }

        /// <summary>3D tour of this layout. Validated by MediaUrlPolicy.</summary>
        public string? Module3DLink { get; set; }
    }

    /// <summary>
    /// Response returned after successfully creating a TypeBien.
    /// </summary>
    public class CreateTypeBienResponse
    {
        public int Id { get; set; }
        public string Message { get; set; }
    }
}
