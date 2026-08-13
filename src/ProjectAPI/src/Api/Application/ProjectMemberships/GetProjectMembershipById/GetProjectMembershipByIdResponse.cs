namespace ProjectAPI.Api.Application.ProjectMemberships.GetProjectMembershipById;

public class GetProjectMembershipByIdResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool IsActive { get; set; }
    public string? AssignedByUserId { get; set; }
    public DateTime AssignedAt { get; set; }
}
