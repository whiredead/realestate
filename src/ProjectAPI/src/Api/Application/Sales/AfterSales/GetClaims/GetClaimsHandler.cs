using ProjectAPI.Api.Application.Common.Units;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Sales.Interfaces;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;
using UnitEntity = ProjectAPI.Domain.Immeubles.Entities.Unit;

namespace ProjectAPI.Api.Application.Sales.AfterSales.GetClaims;

/// <summary>
/// §6.4/§20 — listing used to filter only by caller-supplied query params
/// (BuyerId/AgentId/UnitId/Status), deriving no restriction at all from the
/// caller's own role or project membership: a TECHNICIAN calling with no
/// filters saw every claim in every project, and PROJECT_ADMIN was equally
/// unscoped. Mirrors UpdateClaimStatusHandler's unit → immeuble → project
/// resolution and its "TECHNICIAN sees only claims assigned to them" rule.
/// </summary>
public class GetClaimsHandler : IRequestHandler<GetClaimsQuery, PaginatedResponse<AfterSaleClaimResponse>>
{
    private readonly IAfterSaleClaimRepository _repo;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;
    private readonly ApplicationDbContext _db;

    public GetClaimsHandler(
        IAfterSaleClaimRepository repo,
        ProjectScopeService projectScope,
        ICurrentUser currentUser,
        ApplicationDbContext db)
    {
        _repo = repo;
        _projectScope = projectScope;
        _currentUser = currentUser;
        _db = db;
    }

    public async Task<PaginatedResponse<AfterSaleClaimResponse>> Handle(GetClaimsQuery r, CancellationToken ct)
    {
        // Use GetAllWithAttachmentsAsync to load Attachments collection for the response
        var q = (await _repo.GetAllWithAttachmentsAsync()).AsQueryable();

        if (!string.IsNullOrEmpty(r.BuyerId)) q = q.Where(c => c.BuyerId == r.BuyerId);
        if (!string.IsNullOrEmpty(r.AgentId)) q = q.Where(c => c.AssignedAgentId == r.AgentId);
        if (r.UnitId.HasValue) q = q.Where(c => c.UnitId == r.UnitId.Value);
        if (r.Status.HasValue) q = q.Where(c => c.Status == r.Status.Value);
        if (r.From.HasValue) q = q.Where(c => c.CreatedAt >= r.From.Value);
        if (r.To.HasValue) q = q.Where(c => c.CreatedAt < r.To.Value);

        // §6.1 — TECHNICIAN sees only claims assigned to them, regardless of
        // what AgentId filter (if any) was supplied.
        if (_currentUser.IsInRole(RoleCodes.Technician)
            && !_currentUser.IsInRole(RoleCodes.TechLead)
            && !_currentUser.IsGlobalAdmin
            && !_currentUser.IsInRole(RoleCodes.ProjectAdmin))
        {
            q = q.Where(c => c.AssignedAgentId == _currentUser.UserId);
        }

        // §6.4 — everyone else (PROJECT_ADMIN; GLOBAL_ADMIN is unrestricted)
        // is limited to claims whose unit resolves into their own scoped
        // projects, via the same unit -> immeuble -> project chain
        // UpdateClaimStatusHandler already uses for the same reason.
        var scopedProjectIds = await _projectScope.GetScopedProjectIdsAsync(ct);
        if (scopedProjectIds is not null)
        {
            var scopedUnitIds = await _db.Set<UnitEntity>()
                .Join(_db.Set<Immeuble>(), u => u.ProjectId, im => im.Id, (u, im) => new { u.Id, im.ProjectId })
                .Where(x => scopedProjectIds.Contains(x.ProjectId))
                .Select(x => x.Id)
                .ToListAsync(ct);
            var scopedUnitIdSet = scopedUnitIds.ToHashSet();

            q = q.Where(c => scopedUnitIdSet.Contains(c.UnitId));
        }

        var total = q.Count();

        var data = q
            .OrderByDescending(c => c.CreatedAt)
            .Skip((r.PageNumber - 1) * r.PageSize)
            .Take(r.PageSize)
            .Select(c => new AfterSaleClaimResponse
            {
                Id = c.Id,
                UnitId = c.UnitId,
                Title = c.Title,
                Category = c.Category,
                Priority = c.Priority,
                Status = c.Status,
                CreatedAt = c.CreatedAt,
                ResolvedAt = c.ResolvedAt,
                AssignedAgentId = c.AssignedAgentId,
                AttachmentUrls = c.Attachments.Select(a => a.Url)
            })
            .ToList();

        var locations = await Common.Units.UnitLocations.ForUnitsAsync(_db, data.Select(c => c.UnitId), ct);
        foreach (var claim in data) claim.WithLocation(locations.GetValueOrDefault(claim.UnitId));

        return new PaginatedResponse<AfterSaleClaimResponse>(data, r.PageNumber, r.PageSize, total);
    }
}
