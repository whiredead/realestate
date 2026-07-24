using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Domain.Sales.Entities;

namespace ProjectAPI.Api.Application.Sales.AfterSales.GetClaims;

public class GetClaimsQuery : IRequest<PaginatedResponse<AfterSaleClaimResponse>>
{
    public string? BuyerId { get; set; }
    public string? AgentId { get; set; }
    public Guid? UnitId { get; set; }
    public ClaimStatus? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}