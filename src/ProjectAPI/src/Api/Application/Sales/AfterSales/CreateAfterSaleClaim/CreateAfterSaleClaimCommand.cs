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

    // Attachments are NOT accepted here.
    //
    // This command used to take a List<FileDto> whose Url was a raw
    // client-supplied string, stored verbatim as the attachment's location and
    // rendered as a link: anyone could point a claim attachment at any URL on
    // the internet, and nothing validated the scheme, the host, or that the
    // file had anything to do with this system. Nothing in the frontend ever
    // sent it, so removing it breaks no caller.
    //
    // Photographs of a defect are uploaded as FILES, to a private container,
    // via POST /api/claims/{claimId}/attachments — see
    // UploadClaimAttachmentCommand.
}