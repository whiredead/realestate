namespace ProjectAPI.Api.Application.ProjectMemberships.CreateProjectMembership;

/// <summary>Command to grant a project membership (SALES_AGENT/TECHNICIAN/NOTARY/PROJECT_ADMIN) to a user.</summary>
public class CreateProjectMembershipCommand : IRequest<CreateProjectMembershipResponse>
{
    public Guid ProjectId { get; set; }

    /// <summary>AspNetUsers.Id of the person being granted the membership.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Must be one of RoleCodes.MembershipRoles — GLOBAL_ADMIN is never accepted here.</summary>
    public string RoleCode { get; set; } = string.Empty;

    /// <summary>Defaults to now (UTC) when omitted.</summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>Null means open-ended.</summary>
    public DateTime? ValidUntil { get; set; }
}
