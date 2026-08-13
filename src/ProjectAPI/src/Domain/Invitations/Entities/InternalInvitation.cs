using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Domain.Invitations.Entities;

/// <summary>
/// Phase 2 — the invitation an admin sends to bring a new internal-role
/// account (SALES_AGENT, TECHNICIAN, NOTARY, PROJECT_ADMIN) or GLOBAL_ADMIN
/// onto the platform. Deliberately separate from the buyer-only
/// <see cref="Crm.Entities.AccountInvitation"/>: that one links an existing
/// CrmContact and hands off to the public registration screen; this one
/// grants an internal role and one or more project memberships in a single
/// acceptance action, and the account it produces may not use public
/// registration at all (see AuthenticationAPI.RegisterHandler).
///
/// No stored Status enum — Accepted/Revoked are real one-time events with
/// their own timestamps (cannot be derived), Expired is purely a function of
/// ExpiresAt vs. now (storing it would drift). See <see cref="GetState"/>.
/// </summary>
public class InternalInvitation
{
    public Guid Id { get; set; }

    /// <summary>Normalized lowercase — the lookup key against AuthenticationAPI on accept.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Canonical §6.1 code from RoleCodes.Internal. Exactly one role per invitation.</summary>
    public string RoleCode { get; set; } = string.Empty;

    /// <summary>AspNetUsers.Id of the inviting admin. Always required — unlike ProjectMembership.AssignedByUserId, no migration-backfill exception exists here.</summary>
    public string InvitedByUserId { get; set; } = string.Empty;

    /// <summary>SHA-256 hex digest of the raw token. The raw token itself is never stored — see CreateInternalInvitationHandler.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }
    public string? RevokedByUserId { get; set; }

    public DateTime? AcceptedAt { get; set; }

    /// <summary>AspNetUsers.Id (GPIA_Auth) the acceptance produced — set once AuthenticationAPI confirms provisioning.</summary>
    public string? AcceptedUserId { get; set; }

    public ICollection<InternalInvitationProjectAssignment> ProjectAssignments { get; set; } = new List<InternalInvitationProjectAssignment>();

    /// <summary>
    /// Evaluates the invitation's current state. Accepted/Revoked (real
    /// events) take priority over Expired (a clock comparison): an
    /// invitation accepted before its expiry must always read as Accepted,
    /// never flip to Expired after the fact.
    /// </summary>
    public InvitationState GetState(DateTime asOfUtc)
    {
        if (AcceptedAt is not null) return InvitationState.Accepted;
        if (RevokedAt is not null) return InvitationState.Revoked;
        if (ExpiresAt <= asOfUtc) return InvitationState.Expired;
        return InvitationState.Pending;
    }
}

public enum InvitationState
{
    Pending,
    Accepted,
    Revoked,
    Expired
}
