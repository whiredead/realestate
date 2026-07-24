using ProjectAPI.Domain.Sales.Entities;

namespace ProjectAPI.Api.Application.Sales.AfterSales.CreateAfterSaleClaim;

public class CreateAfterSaleClaimCommand : IRequest<Guid>
{
    public Guid UnitId { get; set; }
    public string? BuyerId { get; set; }           // if logged in buyer
    public string? GuestName { get; set; }         // else use guest fields
    public string? GuestEmail { get; set; }
    public string? GuestPhone { get; set; }

    public string Title { get; set; }
    public string Description { get; set; }
    public ClaimCategory Category { get; set; } = ClaimCategory.General;
    public ClaimPriority Priority { get; set; } = ClaimPriority.Normal;

    public List<FileDto> Files { get; set; } = new();
}
public class FileDto
{
    public string Url { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
}