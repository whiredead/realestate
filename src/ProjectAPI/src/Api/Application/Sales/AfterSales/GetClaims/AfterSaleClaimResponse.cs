using ProjectAPI.Domain.Sales.Entities;

namespace ProjectAPI.Api.Application.Sales.AfterSales.GetClaims;

public class AfterSaleClaimResponse
{
    public Guid Id { get; set; }
    public Guid UnitId { get; set; }
    public string Title { get; set; }
    public ClaimCategory Category { get; set; }
    public ClaimPriority Priority { get; set; }
    public ClaimStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public IEnumerable<string> AttachmentUrls { get; set; } = Enumerable.Empty<string>();
}