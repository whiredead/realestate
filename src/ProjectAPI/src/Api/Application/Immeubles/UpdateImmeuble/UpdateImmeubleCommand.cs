using ProjectAPI.Api.Application.Common.Models;

namespace ProjectAPI.Api.Application.Immeubles.UpdateImmeuble
{
    /// <summary>
    /// Command for updating a immeuble.
    /// </summary>
    public class UpdateImmeubleCommand : IRequest<ImmeubleResponse>
    {
        /// <summary>
        /// Gets or sets the ID of the immeuble to update.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the immeuble.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the location of the immeuble.
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Gets or sets the type of the property.
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
        /// Gets or sets the minimum sellable surface area range of the immeuble.
        /// </summary>
        public int MinSellableSurfaceRange { get; set; }

        /// <summary>
        /// Gets or sets the maximum sellable surface area range of the immeuble.
        /// </summary>
        public int MaxSellableSurfaceRange { get; set; }

        /// <summary>
        /// Gets or sets the status of the immeuble.
        /// </summary>
        public ProjectStatus? Status { get; set; }

        /// <summary>
        /// Gets or sets the images of the immeuble.
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
    }
}
