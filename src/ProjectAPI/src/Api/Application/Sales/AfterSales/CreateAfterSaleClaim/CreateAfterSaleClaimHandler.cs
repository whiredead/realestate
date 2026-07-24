using ProjectAPI.Domain.Purchases.Interfaces;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Domain.Sales.Interfaces;

namespace ProjectAPI.Api.Application.Sales.AfterSales.CreateAfterSaleClaim;

public class CreateAfterSaleClaimHandler : IRequestHandler<CreateAfterSaleClaimCommand, Guid>
{
    private readonly IAfterSaleClaimRepository _claimRepo;
    private readonly IClaimAttachmentRepository _attachRepo;
    private readonly IPurchaseRepository _purchaseRepo; // to validate ownership

    public CreateAfterSaleClaimHandler(
        IAfterSaleClaimRepository claimRepo,
        IClaimAttachmentRepository attachRepo,
        IPurchaseRepository purchaseRepo)
    {
        _claimRepo = claimRepo;
        _attachRepo = attachRepo;
        _purchaseRepo = purchaseRepo;
    }

    public async Task<Guid> Handle(CreateAfterSaleClaimCommand r, CancellationToken ct)
    {
        // Optional: validate buyer really owns (via Purchase -> Reservation -> Unit)
        Guid? purchaseId = null;
        if (!string.IsNullOrEmpty(r.BuyerId))
        {
            var buyerPurchases = await _purchaseRepo.Find(p => p.UserId == r.BuyerId && p.ReservationId != null);
            var ownsUnit = buyerPurchases.Any(); // refine if you can trace Unit via Reservation/Unit
            if (!ownsUnit)
                throw new ValidationException("This user does not own a unit linked to this project/unit.");
            purchaseId = buyerPurchases.First().Id;
        }

        var claim = new AfterSaleClaim
        {
            Id = Guid.NewGuid(),
            UnitId = r.UnitId,
            PurchaseId = purchaseId,
            BuyerId = r.BuyerId,
            GuestName = r.BuyerId == null ? r.GuestName : null,
            GuestEmail = r.BuyerId == null ? r.GuestEmail : null,
            GuestPhone = r.BuyerId == null ? r.GuestPhone : null,
            Title = r.Title,
            Description = r.Description,
            Category = r.Category,
            Priority = r.Priority,
            Status = ClaimStatus.New,
            CreatedAt = DateTime.UtcNow
        };

        await _claimRepo.InsertAsync(claim);

        foreach (var f in r.Files ?? [])
        {
            await _attachRepo.InsertAsync(new ClaimAttachment
            {
                Id = Guid.NewGuid(),
                ClaimId = claim.Id,
                Url = f.Url,
                FileName = f.FileName,
                ContentType = f.ContentType,
                SizeBytes = f.SizeBytes
            });
        }

        await _claimRepo.SaveAsync();
        await _attachRepo.SaveAsync();

        return claim.Id;
    }
}
