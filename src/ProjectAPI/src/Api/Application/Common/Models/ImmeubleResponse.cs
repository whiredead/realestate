using ProjectAPI.Api.Application.TypeBiens.GetTypeBiensByImmeuble;
using System.Text.Json.Serialization;

namespace ProjectAPI.Api.Application.Common.Models
{
    /// <summary>
    /// Enumeration for the type of property.
    /// </summary>
   // public enum PropertyType { Villa, Apartment, Commercial }

    /// <summary>
    /// Represents the response model for project details.
    /// </summary>
    public class ImmeubleResponse
    {
        /// <summary>
        /// Gets or sets the unique identifier for the project.
        /// </summary>
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string? AgentId { get; set; }

        /// <summary>
        /// Gets or sets the name of the project.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the location of the project.
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Gets or sets the type of property (e.g., Villa, Apartment, Commercial).
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the minimum price for properties in the project.
        /// </summary>
        public decimal MinPrice { get; set; }

        /// <summary>
        /// Gets or sets the maximum price for properties in the project.
        /// </summary>
        public decimal MaxPrice { get; set; }

        /// <summary>
        /// Canonical lifecycle code (§3), normalized from the free-text
        /// Immeuble.Status column via ProjectStatusCodes.Normalize — same
        /// pattern as Project.StatusGlobal. Not a fixed enum: that broke on
        /// every canonical/unrecognized value (see the removed ProjectStatus
        /// enum, which silently mapped anything unparsed to "ComingSoon" in
        /// GetAllImmeublesHandler and threw outright in GetImmeubleByIdHandler
        /// and UpdateImmeubleHandler).
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Gets or sets the image URLs for the project, represented as JSON.
        /// </summary>
        public List<string> Images { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the description of the project.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the number of units in the project.
        /// </summary>
        public int NumberOfUnits { get; set; }

        /// <summary>
        /// Gets or sets the minimum sellable surface area range of the project.
        /// </summary>
        [JsonIgnore] 
        public int MinSellableSurfaceRange { get; set; }

        /// <summary>
        /// Gets or sets the maximum sellable surface area range of the project.
        /// </summary>
        [JsonIgnore] 
        public int MaxSellableSurfaceRange { get; set; }

        /// <summary>
        /// Gets or sets the sellable surface area range of the project.
        /// </summary>
        public string SellableSurfaceRange => $"De {MinSellableSurfaceRange} à {MaxSellableSurfaceRange} m2";

        public string? Module3DLink { get; set; }

        public int NumberOfSoldUnites { get; set; }

        /// <summary>
        /// Gets or sets the total number of units still available in the Immeuble.
        /// </summary>
        public int NumberOfAvailableUnites { get; set; }

        /// <summary>
        /// Units spoken for but not yet sold: HoldPendingApproval, Reserved and
        /// Contracted. Without this the three published figures don't add up to
        /// NumberOfUnits — a 66-unit building reporting 44 sold and 9 available
        /// left 13 units unaccounted for on every screen that showed it.
        /// </summary>
        public int NumberOfReservedUnites { get; set; }

        [JsonIgnore] 
        public int SellsPercentage { get; set; }
        public string RestPercentage => $"{SellsPercentage} %";

        public ICollection<PlanInerieurResponse>? PlanInerieurs { get;set; }
        public ICollection<TypeBienListItem>? TypeBiens { get; set; }
    }
}
