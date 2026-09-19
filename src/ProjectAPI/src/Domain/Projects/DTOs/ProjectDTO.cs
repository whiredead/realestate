namespace ProjectAPI.Domain.Projects.DTOs;

public class ProjectDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Location { get; set; }
    public string Address { get; set; }
    public List<string> Images { get; set; } = new();
    public string Description { get; set; }
    public string Module3DLink { get; set; }
    public string? Type { get; set; }
    public string? StatusGlobal { get; set; }
    public string? StatusReferenceCode { get; set; }
    public decimal OverAllProgress { get; set; }
    public long NumberLikes { get; set; }
    public int WarrantyMonths { get; set; }
    public Guid? QuartierId { get; set; }
    public string? QuartierName { get; set; }
    public string? QuartierDescription { get; set; }
    public string? QuartierImages { get; set; }
    public bool IsLiked { get; set; }
    public string? AgentId { get; set; }
    public string? AgentPhoneNumber { get; set; }
    public string? NotaryId { get; set; }
    public string? NotaryPhoneNumber { get; set; }
    public List<TypeBienDTO> TypeBiens { get; set; } = new();
    /// <summary>
    /// Gets or sets the list of agents assigned to the project.
    /// </summary>
    public List<AgentDTO> AssignedAgents { get; set; } = new();
    public List<NotaryDTO> AssignedNotaries { get; set; } = new();
}
public class TypeBienDTO
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? Image { get; set; }
    public double? Price { get; set; }
    public int? NbrChambre { get; set; }
    public int? NbrSalleDeBain { get; set; }
    public int? NbrDouche { get; set; }
    public int? NbrParking { get; set; }
    public string? Module3DLink { get; set; }
    public int? MinSurface { get; set; }
    public int? MaxSurface { get; set; }
    public string SurfaceRange { get; set; }
    public string? ImagesInterieur { get; set; }
}
