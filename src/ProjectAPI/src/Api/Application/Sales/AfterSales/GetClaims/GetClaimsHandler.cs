using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Domain.Sales.Interfaces;

namespace ProjectAPI.Api.Application.Sales.AfterSales.GetClaims;

public class GetClaimsHandler : IRequestHandler<GetClaimsQuery, PaginatedResponse<AfterSaleClaimResponse>>
{
    private readonly IAfterSaleClaimRepository _repo;

    public GetClaimsHandler(IAfterSaleClaimRepository repo) => _repo = repo;

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
                AttachmentUrls = c.Attachments.Select(a => a.Url)
            })
            .ToList();

        return new PaginatedResponse<AfterSaleClaimResponse>(data, r.PageNumber, r.PageSize, total);
    }
}
