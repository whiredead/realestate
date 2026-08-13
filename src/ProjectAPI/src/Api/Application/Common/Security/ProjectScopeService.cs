using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Common.Security;

/// <summary>
/// Enforces the project perimeter required by spec §6.4:
/// "Chaque requête portant sur une ressource projet vérifie l'affectation de
/// l'utilisateur."
///
/// Role alone is never sufficient below GLOBAL_ADMIN. An agent assigned to
/// project A must not act on project B even though both are "SALES_AGENT"
/// requests, which is exactly what a role-only <c>[Authorize]</c> would allow.
///
/// Phase 1 — scope resolution reads <see cref="ProjectMembership"/>, the
/// generic replacement for the old agent/notary-only
/// <see cref="ProjectAssignment"/> table. A membership only grants access
/// while it is BOTH <c>IsActive</c> AND inside its
/// [<c>ValidFrom</c>, <c>ValidUntil</c>) window — the two are independent:
/// IsActive is an administrative on/off switch, the window is a time-based
/// one, and a caller must clear both.
/// </summary>
public class ProjectScopeService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _user;

    public ProjectScopeService(ApplicationDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    /// <summary>
    /// Projects the caller may act on, or <c>null</c> meaning "all projects"
    /// (GLOBAL_ADMIN only, §6.3). Callers must treat null as unrestricted.
    /// </summary>
    public async Task<HashSet<Guid>?> GetScopedProjectIdsAsync(CancellationToken ct)
    {
        if (_user.IsGlobalAdmin) return null;

        var userId = _user.UserId;
        if (string.IsNullOrEmpty(userId)) return new HashSet<Guid>();

        // "Now" is always server UTC — ValidFrom/ValidUntil are stored and
        // compared in UTC consistently; see ProjectMembership's doc comments.
        var now = DateTime.UtcNow;

        var ids = await _db.Set<ProjectMembership>()
            .Where(m => m.UserId == userId
                     && m.IsActive
                     && m.ValidFrom <= now
                     && (m.ValidUntil == null || m.ValidUntil > now))
            .Select(m => m.ProjectId)
            .Distinct()
            .ToListAsync(ct);

        return ids.ToHashSet();
    }

    /// <summary>True when the caller may act on the given project.</summary>
    public async Task<bool> CanAccessProjectAsync(Guid projectId, CancellationToken ct)
    {
        var scope = await GetScopedProjectIdsAsync(ct);
        return scope is null || scope.Contains(projectId);
    }

    /// <summary>
    /// Throws <c>403 PROJECT_SCOPE_DENIED</c> when the project is outside the
    /// caller's perimeter. Use this in handlers that mutate project data.
    /// </summary>
    public async Task EnsureProjectAccessAsync(Guid projectId, CancellationToken ct)
    {
        if (!await CanAccessProjectAsync(projectId, ct))
        {
            throw BusinessRuleException.ProjectScopeDenied(projectId);
        }
    }

    /// <summary>
    /// Resolves the project a reservation belongs to, following
    /// reservation → unit → immeuble → project, then applies the perimeter
    /// check. Returns silently for a GLOBAL_ADMIN.
    ///
    /// A caller with no internal role (BUYER/PROSPECT) has no project
    /// perimeter at all — their access is per-file, via
    /// <see cref="EnsureBuyerOwnsReservationAsync"/>, not per-project. Applying
    /// the project check to them here would reject every buyer outright, since
    /// they hold no <see cref="Domain.Projects.Entities.ProjectMembership"/>
    /// row to begin with.
    /// </summary>
    public async Task EnsureReservationAccessAsync(Guid reservationId, CancellationToken ct)
    {
        if (_user.IsGlobalAdmin) return;

        var isInternal = _user.Roles.Any(r => RoleCodes.Internal.Contains(r, StringComparer.Ordinal));
        if (!isInternal) return;

        var projectId = await (
            from r in _db.Set<Reservation>()
            join u in _db.Set<Domain.Immeubles.Entities.Unit>() on r.UnitId equals u.Id
            join im in _db.Set<Domain.Immeubles.Entities.Immeuble>() on u.ProjectId equals im.Id
            where r.Id == reservationId
            select im.ProjectId).FirstOrDefaultAsync(ct);

        if (projectId == Guid.Empty)
        {
            // Unresolvable ownership: fail closed rather than assume access.
            throw BusinessRuleException.ProjectScopeDenied(projectId);
        }

        await EnsureProjectAccessAsync(projectId, ct);
    }

    /// <summary>
    /// Resolves the project a unit belongs to (unit → immeuble → project) and
    /// applies the perimeter check for internal roles. Unlike
    /// <see cref="EnsureReservationAccessAsync"/>, a buyer/prospect caller is
    /// NOT waved through here — this only covers the internal-role half of
    /// the check. Callers reachable by buyers (e.g. a unit's title-status
    /// read, shown on both an internal unit page and a buyer's own property
    /// file) must also call <see cref="EnsureBuyerOwnsUnitAsync"/> for that
    /// caller shape; the two are deliberately separate so a caller that only
    /// makes sense for staff doesn't accidentally admit any buyer at all.
    /// </summary>
    public async Task EnsureUnitProjectAccessAsync(Guid unitId, CancellationToken ct)
    {
        if (_user.IsGlobalAdmin) return;

        var isInternal = _user.Roles.Any(r => RoleCodes.Internal.Contains(r, StringComparer.Ordinal));
        if (!isInternal) return;

        var projectId = await (
            from u in _db.Set<Domain.Immeubles.Entities.Unit>()
            join im in _db.Set<Domain.Immeubles.Entities.Immeuble>() on u.ProjectId equals im.Id
            where u.Id == unitId
            select im.ProjectId).FirstOrDefaultAsync(ct);

        if (projectId == Guid.Empty)
        {
            // Unresolvable ownership: fail closed rather than assume access.
            throw BusinessRuleException.ProjectScopeDenied(projectId);
        }

        await EnsureProjectAccessAsync(projectId, ct);
    }

    /// <summary>
    /// §6.4 — a buyer/prospect may read a unit's data only when they hold a
    /// reservation on it (any status, since a title-status read is harmless
    /// context, not a mutation). Internal roles are checked separately by
    /// <see cref="EnsureUnitProjectAccessAsync"/>.
    /// </summary>
    public async Task EnsureBuyerOwnsUnitAsync(Guid unitId, CancellationToken ct)
    {
        var isBuyerOnly = !_user.Roles.Any(r => RoleCodes.Internal.Contains(r, StringComparer.Ordinal));
        if (!isBuyerOnly) return;

        var userId = _user.UserId;
        var owns = await _db.Set<Reservation>()
            .AnyAsync(r => r.UnitId == unitId && r.BuyerId == userId, ct);

        if (!owns)
        {
            throw BusinessRuleException.BuyerScopeDenied();
        }
    }

    /// <summary>
    /// §6.3 — "Un notaire gère uniquement son propre calendrier" (weekly
    /// availability + blocks). A GLOBAL_ADMIN/PROJECT_ADMIN caller may manage
    /// any notary's calendar (that is the "admins oversee" half of the
    /// controller's [Authorize(Roles = AdminsNotary)] gate); a NOTARY caller
    /// must be managing their own — the {notaryId} route segment is
    /// caller-supplied and must never be trusted to equal the caller's own id.
    /// </summary>
    public void EnsureNotaryOwnsCalendar(string targetNotaryId)
    {
        if (_user.IsGlobalAdmin || _user.IsInRole(RoleCodes.ProjectAdmin)) return;

        if (_user.IsInRole(RoleCodes.Notary) && !string.Equals(_user.UserId, targetNotaryId, StringComparison.Ordinal))
        {
            throw BusinessRuleException.NotaryCalendarScopeDenied();
        }
    }

    /// <summary>
    /// §6.3 — "Un agent gère uniquement son propre calendrier" (weekly
    /// availability, blocks, date overrides, appointment settings). Mirrors
    /// <see cref="EnsureNotaryOwnsCalendar"/> exactly: a GLOBAL_ADMIN/
    /// PROJECT_ADMIN caller may manage any agent's calendar; a SALES_AGENT
    /// caller must be managing their own — the {agentId} route segment is
    /// caller-supplied and must never be trusted to equal the caller's own id.
    /// </summary>
    public void EnsureAgentOwnsCalendar(string targetAgentId)
    {
        if (_user.IsGlobalAdmin || _user.IsInRole(RoleCodes.ProjectAdmin)) return;

        if (_user.IsInRole(RoleCodes.SalesAgent) && !string.Equals(_user.UserId, targetAgentId, StringComparison.Ordinal))
        {
            throw BusinessRuleException.AgentCalendarScopeDenied();
        }
    }

    /// <summary>
    /// §6.4 — "Un acheteur ne peut accéder qu'à ses propres données".
    /// Throws when the caller is a BUYER/PROSPECT acting on someone else's
    /// reservation. Internal roles are checked by perimeter instead.
    /// </summary>
    public async Task EnsureBuyerOwnsReservationAsync(Guid reservationId, CancellationToken ct)
    {
        var isBuyerOnly = !_user.Roles.Any(r => RoleCodes.Internal.Contains(r, StringComparer.Ordinal));
        if (!isBuyerOnly) return;

        var userId = _user.UserId;
        var owns = await _db.Set<Reservation>()
            .AnyAsync(r => r.Id == reservationId && r.BuyerId == userId, ct);

        if (!owns)
        {
            throw BusinessRuleException.BuyerScopeDenied();
        }
    }
}
