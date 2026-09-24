using ProjectAPI.Api.Application.TypeBiens.GetTypeBiensByImmeuble;
using ProjectAPI.Domain.Projects.DTOs;

public class ProjectResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Location { get; set; }
    public string Address { get; set; }
    public string? AgentId { get; set; }
    public string? AgentPhoneNumber { get; set; }
    public List<string> Images { get; set; } = new();
    public string Description { get; set; }
    public string Module3DLink { get; set; }
    public string? Type { get; set; }
    public string? StatusGlobal { get; set; }
    public decimal OverAllProgress { get; set; }
    public long NumberLikes { get; set; }

    /// <summary>§8 — warranty granted on this project's units, in months.</summary>
    public int WarrantyMonths { get; set; }

    /// <summary>Needed by the console to edit the quartier itself, not just show its name.</summary>
    public Guid? QuartierId { get; set; }
    public string? QuartierName { get; set; }
    public string? QuartierDescription { get; set; }
    public string? QuartierImages { get; set; }
    public bool IsLiked { get; set; }
    public List<TypeBienListItem> TypeBiens { get; set; } = new();
    public List<AgentDTO> AssignedAgents { get; set; } = new();
    public List<NotaryDTO> AssignedNotaries { get; set; } = new();

}
