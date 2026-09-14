using ProjectAPI.Api.Application.Common.Units;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Sales.AfterSales.GetMyClaims;

public class GetMyClaimsHandler : IRequestHandler<GetMyClaimsQuery, PaginatedResponse<MyClaimSummary>>
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public GetMyClaimsHandler(ApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResponse<MyClaimSummary>> Handle(GetMyClaimsQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new PaginatedResponse<MyClaimSummary>(new List<MyClaimSummary>(), request.PageNumber, request.PageSize, 0);
        }

        var query = _db.Set<AfterSaleClaim>()
            .Include(c => c.Attachments)
            .Where(c => c.BuyerId == userId)
            .OrderByDescending(c => c.CreatedAt);

        var total = await query.CountAsync(ct);

        var data = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new MyClaimSummary
            {
                Id = c.Id,
                UnitId = c.UnitId,
                Title = c.Title,
                Description = c.Description,
                Category = (int)c.Category,
                Priority = (int)c.Priority,
                Status = (int)c.Status,
                CreatedAt = c.CreatedAt,
                ResolvedAt = c.ResolvedAt,
                AttachmentUrls = c.Attachments.Select(a => a.Url).ToList()
            })
            .ToListAsync(ct);

        var locations = await Common.Units.UnitLocations.ForUnitsAsync(_db, data.Select(c => c.UnitId), ct);
        foreach (var claim in data) claim.WithLocation(locations.GetValueOrDefault(claim.UnitId));

        return new PaginatedResponse<MyClaimSummary>(data, request.PageNumber, request.PageSize, total);
    }
}
