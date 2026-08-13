namespace ProjectAPI.Api.Application.Leads.GetLeads;

public class LeadResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserFullName { get; set; }
    public string? AgentId { get; set; }
    public string? AgentFullName { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}
