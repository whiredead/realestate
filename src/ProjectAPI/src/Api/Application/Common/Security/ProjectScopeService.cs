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
/// Scope resolution reuses the existing <see cref="ProjectAssignment"/> table
/// (agent/notary columns) rather than introducing a parallel one — the data is
/// already maintained by the Assignments screen.
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

        var ids = await _db.Set<ProjectAssignment>()
            .Where(a => a.IsActive && (a.AgentId == userId || a.NotaryId == userId))
            .Select(a => a.ProjectId)
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
    /// </summary>
    public async Task EnsureReservationAccessAsync(Guid reservationId, CancellationToken ct)
    {
        if (_user.IsGlobalAdmin) return;

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
            throw BusinessRuleException.ProjectScopeDenied(Guid.Empty);
        }
    }
}
