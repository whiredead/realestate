using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Domain.Immeubles.Entities
{
    /// <summary>
    /// Represents a real estate Immeuble, including details such as its name, location, type, pricing, associated units, and status.
    /// </summary>
    public class Immeuble
    {
        /// <summary>
        /// Gets or sets the unique identifier of the Immeuble.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the name of the Immeuble.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the location of the Immeuble.
        /// </summary>
        public string? Location { get; set; }

        /// <summary>
        /// Gets or sets the type of the Immeuble (e.g., F1, F2, F3,...).
        /// </summary>
        public string? Type { get; set; }
        /// <summary>
        /// Gets or sets the type of the Immeuble (e.g., Commercial, Villa, Appartement,...).
        /// </summary>
        public string? ResidencyType { get; set; }

        /// <summary>
        /// Gets or sets the minimum price of properties in the Immeuble.
        /// </summary>
        public decimal MinPrice { get; set; }

        /// <summary>
        /// Gets or sets the maximum price of properties in the Immeuble.
        /// </summary>
        public decimal MaxPrice { get; set; }

        /// <summary>
        /// Gets or sets the image URLs associated with the Immeuble.
        /// </summary>
        public string? Images { get; set; }
        public string? ImagePrincipale { get; set; }
        /// <summary>
        /// Gets or sets the 3D module link for the Immeuble visualization.
        /// </summary>
        public string? Module3DLink { get; set; }

        /// <summary>
        /// Gets or sets the description of the Immeuble.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the latitude of the Immeuble's location.
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// Gets or sets the longitude of the Immeuble's location.
        /// </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// Gets or sets the status of the Immeuble (e.g., ComingSoon, Available).
        /// </summary>
        public string Status { get; set; }
        public string? AgentId { get; set; }

        /// <summary>
        /// Gets or sets the collection of assignments associated with the Immeuble, representing agents and their responsibilities.
        /// </summary>
        public ICollection<ImmeubleAssignment> Assignments { get; set; }
        public Agent Agent { get; set; }

        /// <summary>
        /// Gets or sets the collection of appointments associated with the Immeuble, representing user interactions.
        /// </summary>
        public ICollection<Appointment> Appointments { get; set; }
        public ICollection<ImmeublePlanInterieur> PlanInterieurs { get; set; }
        /// <summary>
        /// Gets or sets the collection of features associated with the immeuble.
        /// </summary>
        public ICollection<ImmeubleFeature> Features { get; set; } = new List<ImmeubleFeature>();
        public ICollection<ImmeubleTypeBien> TypeBiens { get; set; } = new List<ImmeubleTypeBien>();


        // Foreign key to Project
        public Guid ProjectId { get; set; }
        public Project Project { get; set; }

        /// <summary>
        /// Gets or sets the collection of units available in the Immeuble.
        /// </summary>
        public ICollection<Unit> Units { get; set; } = new List<Unit>();

        /// <summary>
        /// Gets or sets the total number of units available in the Immeuble.
        /// </summary>
        public int NumberOfUnits { get; set; }

        /// <summary>
        /// Gets or sets the total number of units sold in the Immeuble.
        /// </summary>
        public int NumberOfSoldUnites { get; set; }

        /// <summary>
        /// Gets or sets the total number of units still available in the Immeuble.
        /// </summary>
        public int NumberOfAvailableUnites { get; set; }

        /// <summary>
        /// Gets or sets the percentage of units sold in the Immeuble.
        /// </summary>
        public int SellsPercentage { get; set; }

        /// <summary>
        /// Gets or sets the minimum sellable surface area in square meters for the Immeuble.
        /// </summary>
        public int MinSellableSurfaceRange { get; set; }

        /// <summary>
        /// Gets or sets the maximum sellable surface area in square meters for the Immeuble.
        /// </summary>
        public int MaxSellableSurfaceRange { get; set; }
    }
}
