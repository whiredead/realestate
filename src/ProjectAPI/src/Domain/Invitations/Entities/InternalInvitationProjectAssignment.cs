using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Domain.Invitations.Entities;

/// <summary>
/// One pending project grant on an <see cref="InternalInvitation"/> — a
/// direct, typed mirror of the <see cref="ProjectMembership"/> row it will
/// become on acceptance (see AcceptInternalInvitationHandler). Modeled as a
/// child table, not a JSON/CSV column, so the FK to <see cref="Project"/> is
/// database-enforced rather than trusted from an unvalidated blob.
/// </summary>
public class InternalInvitationProjectAssignment
{
    public Guid Id { get; set; }

    public Guid InternalInvitationId { get; set; }
    public InternalInvitation InternalInvitation { get; set; } = null!;

    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
}
