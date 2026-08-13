using ProjectAPI.Api.Application.Common.Models;

namespace ProjectAPI.Api.Application.Sales.AfterSales.GetMyClaims;

/// <summary>§8 "SAV" — the buyer's own claims only. See GetMyReservationsQuery for why this exists alongside GetClaimsQuery.</summary>
public class GetMyClaimsQuery : IRequest<PaginatedResponse<MyClaimSummary>>
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class MyClaimSummary
{
    public Guid Id { get; set; }
    public Guid UnitId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Category { get; set; }
    public int Priority { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public List<string> AttachmentUrls { get; set; } = new();
}
