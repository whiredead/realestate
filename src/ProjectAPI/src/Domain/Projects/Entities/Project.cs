using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Immeubles.Entities;

namespace ProjectAPI.Domain.Projects.Entities;

/// <summary>
/// Represents a real estate project containing multiple Immeubles.
/// </summary>
public class Project
{
    /// <summary>
    /// Gets or sets the unique identifier of the project.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the agent associated to the porject.
    /// </summary>
    public string? AgentId { get; set; }
    /// <summary>
    /// Gets or sets the name of the project.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the location of the project.// coordiantes x,y
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// Gets or sets the address of the project.
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Gets or sets a detailed description of the project.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the 3D module link for the project visualization. //lien pour 3D module
    /// </summary>
    public string Module3DLink { get; set; }

    /// <summary>
    /// Gets or sets the global status of the project (SUR_PLAN, EN_LIVRAISON or FINALISE — see ProjectStatusCodes).
    /// </summary>
    public string StatusGlobal { get; set; } = "SUR_PLAN";

    /// <summary>
    /// Gets or sets the type of the project (e.g., "Livraison immédiate", "Vente sur plan", 
    /// "Magasin et Commerce", or "Lots de terrains").
    /// </summary>
    public string? Type { get; set; } = "Livraison immédiate";

    /// <summary>
    /// Gets or sets the overall progress of the project in percentage (0 - 100).
    /// </summary>
    public decimal OverAllProgress { get; set; } = 0;

    /// <summary>
    /// Gets or sets the unique identifier of the quartier (district/area) 
    /// to which this project belongs, if any.
    /// </summary>
    public Guid? QuartierId { get; set; }

    /// <summary>
    /// Gets or sets the number of likes for this project.
    /// </summary>
    public long NumberLikes { get; set; } = 0;

    /// <summary>
    /// Warranty length in months granted on every unit of this project (§8).
    ///
    /// This is the ONLY authoritative source: a sale copies it at creation and
    /// the handover reads the frozen copy from the sale, so changing it here
    /// never shortens or extends a warranty already granted. 12 months is the
    /// legacy behaviour <c>StartHandoverHandler</c> hard-coded, kept as the
    /// default so existing projects are unaffected.
    /// </summary>
    public int WarrantyMonths { get; set; } = 12;

    /// <summary>
    /// Gets or sets the list of image URLs associated with the project.
    /// </summary>
    public List<string> Images { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the collection of Immeubles (buildings or complexes) in this project.
    /// </summary>
    public ICollection<Immeuble> Immeubles { get; set; } = new List<Immeuble>();

    /// <summary>
    /// Gets or sets the collection of appointments associated with the project.
    /// </summary>
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    /// <summary>
    /// Gets or sets the collection of features associated with the project (e.g., Swimming Pool, Mosque).
    /// </summary>
    public ICollection<ProjectFeature> Features { get; set; } = new List<ProjectFeature>();
    public ICollection<EspaceTempsReel> Videos { get; set; } = new List<EspaceTempsReel>();
    public ICollection<ProjectAssignment> Assignments { get; set; } = new List<ProjectAssignment>();
    public ICollection<ProjectMembership> Memberships { get; set; } = new List<ProjectMembership>();
    public ICollection<ProjectTypeBien> TypeBiens { get; set; } = new List<ProjectTypeBien>();

    /// <summary>
    /// Gets or sets the quartier (district/area) navigation property for this project.
    /// </summary>
    public Quartier Quartier { get; set; }
}
