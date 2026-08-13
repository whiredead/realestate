using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Domain.Projects.Entities;

/// <summary>
/// Phase 1 — the generic replacement for the agent/notary-only
/// <see cref="ProjectAssignment"/> shape. One row is "this user holds this
/// role on this project, for this validity window."
///
/// <see cref="RoleCode"/> is the canonical §6.1 code (see
/// <c>ProjectAPI.Domain.Users.Entities.RoleCodes</c>) — SALES_AGENT,
/// TECHNICIAN, NOTARY, or PROJECT_ADMIN. GLOBAL_ADMIN never gets a row here:
/// it is unrestricted platform-wide (see ProjectScopeService.IsGlobalAdmin),
/// so a membership row for it would be meaningless.
///
/// At most one ACTIVE row may exist per (UserId, ProjectId, RoleCode) — see
/// ProjectMembershipConfiguration's filtered unique index. That index is the
/// authoritative guard against a race creating two active rows for the same
/// key; it says nothing about ValidFrom/ValidUntil overlap, which only the
/// write path (not a static index) can reason about — see
/// ProjectMembershipConfiguration's remarks for why.
/// </summary>
public class ProjectMembership
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// <summary>AspNetUsers.Id of the member. Agent/Notary/User all share that table (TPH) — see RoleCode for which role this row grants.</summary>
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;

    /// <summary>Canonical §6.1 role code this membership grants on <see cref="ProjectId"/>.</summary>
    public string RoleCode { get; set; } = string.Empty;

    /// <summary>UTC. When this membership starts granting access.</summary>
    public DateTime ValidFrom { get; set; }

    /// <summary>UTC. Null means open-ended.</summary>
    public DateTime? ValidUntil { get; set; }

    /// <summary>
    /// Administrative on/off switch, independent of the validity window — an
    /// admin can deactivate a membership immediately without having to know
    /// or set an end date. Scope resolution requires BOTH IsActive and
    /// "now" falling inside [ValidFrom, ValidUntil).
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// AspNetUsers.Id of the admin who created this membership. Null ONLY
    /// for rows created by the Phase 1 data migration backfilling the old
    /// ProjectAssignment table — no acting admin exists for that history.
    /// Every membership created through the application from this point on
    /// MUST supply this.
    /// </summary>
    public string? AssignedByUserId { get; set; }
    public User? AssignedByUser { get; set; }

    /// <summary>UTC. When this membership was created (distinct from ValidFrom, which may be backdated or future-dated relative to AssignedAt).</summary>
    public DateTime AssignedAt { get; set; }
}
