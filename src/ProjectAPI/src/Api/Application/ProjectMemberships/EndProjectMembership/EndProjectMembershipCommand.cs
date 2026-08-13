namespace ProjectAPI.Api.Application.ProjectMemberships.EndProjectMembership;

/// <summary>Deactivates a membership — the supported way to revoke access; the row is kept for audit history, never deleted.</summary>
public class EndProjectMembershipCommand : IRequest<EndProjectMembershipResponse>
{
    public Guid Id { get; set; }
}
