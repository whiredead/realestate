using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Purchases.Interfaces;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Domain.Sales.Interfaces;
using ProjectAPI.Infrastructure.Context;
using ValidationException = FluentValidation.ValidationException;
using UnitEntity = ProjectAPI.Domain.Immeubles.Entities.Unit;

namespace ProjectAPI.Api.Application.Sales.AfterSales.CreateAfterSaleClaim;

/// <summary>
/// Opens a SAV/warranty claim (spec §5.9, §20).
///
/// Only for a unit that is DELIVERED and within an active warranty — a claim
/// on a unit that never reached delivery has nothing to warrant. The claim
/// starts SUBMITTED; a project admin qualifies it into UNDER_REVIEW and
/// beyond via UpdateClaimStatusHandler.
/// </summary>
public class CreateAfterSaleClaimHandler : IRequestHandler<CreateAfterSaleClaimCommand, Guid>
{
    private readonly IAfterSaleClaimRepository _claimRepo;
    private readonly IClaimAttachmentRepository _attachRepo;
    private readonly IPurchaseRepository _purchaseRepo;
    private readonly ApplicationDbContext _db;

    public CreateAfterSaleClaimHandler(
        IAfterSaleClaimRepository claimRepo,
        IClaimAttachmentRepository attachRepo,
        IPurchaseRepository purchaseRepo,
        ApplicationDbContext db)
    {
        _claimRepo = claimRepo;
        _attachRepo = attachRepo;
        _purchaseRepo = purchaseRepo;
        _db = db;
    }

    public async Task<Guid> Handle(CreateAfterSaleClaimCommand r, CancellationToken ct)
    {
        // §5.9/§6.3 — a warranty claim must reference a DELIVERED unit.
        var unit = await _db.Set<UnitEntity>().FirstOrDefaultAsync(u => u.Id == r.UnitId, ct)
            ?? throw new NotFoundException($"Unit {r.UnitId} not found.");

        if (unit.Status != UnitCommercialStatus.Delivered)
        {
            throw BusinessRuleException.PropertyNotDelivered(r.UnitId);
        }

        // §5.9 — an active warranty is a prerequisite, not just a display fact.
        var warranty = await _db.Warranties
            .Where(w => w.UnitId == r.UnitId && w.IsActive && w.EndsAt > DateTime.UtcNow)
            .OrderByDescending(w => w.StartsAt)
            .FirstOrDefaultAsync(ct);

        if (warranty is null)
        {
            throw BusinessRuleException.WarrantyExpired(r.UnitId);
        }

        Guid? purchaseId = null;
        if (!string.IsNullOrEmpty(r.BuyerId))
        {
            // §5.9/§6.4 — must actually own THIS unit, not merely own some
            // purchase somewhere (the original bug this session fixed). But
            // the legacy Purchase row is only ever created by
            // ApproveReservationHandler, and only when the buyer already had
            // an account AT THE MOMENT OF APPROVAL (hasBuyerAccount). A buyer
            // invited and activated afterward (§1.1/§6.2 Phase 2 — N20) never
            // gets a retroactive Purchase row, so this check had nothing to
            // match against for the exact case it exists to protect: a real,
            // delivered, warrantied owner. Reservation.BuyerId is the current,
            // always-populated signal (set at submit, or backfilled on
            // invitation acceptance) — check that directly instead of the
            // Purchase table, which is a display convenience, not the source
            // of truth for ownership.
            var ownsUnit = await _db.Set<Reservation>().AnyAsync(res =>
                res.BuyerId == r.BuyerId && res.UnitId == r.UnitId, ct);
            if (!ownsUnit)
                throw new ValidationException("This user does not own a unit linked to this project/unit.");

            var buyerPurchases = await _purchaseRepo.Find(p =>
                p.UserId == r.BuyerId
                && ((p.Reservation != null && p.Reservation.UnitId == r.UnitId)
                    || (p.Sale != null && p.Sale.UnitId == r.UnitId)));
            purchaseId = buyerPurchases.FirstOrDefault()?.Id;
        }

        var claim = new AfterSaleClaim
        {
            Id = Guid.NewGuid(),
            UnitId = r.UnitId,
            PurchaseId = purchaseId,
            WarrantyId = warranty.Id,
            BuyerId = r.BuyerId,
            GuestName = r.BuyerId == null ? r.GuestName : null,
            GuestEmail = r.BuyerId == null ? r.GuestEmail : null,
            GuestPhone = r.BuyerId == null ? r.GuestPhone : null,
            Title = r.Title,
            Description = r.Description,
            Category = r.Category,
            Priority = r.Priority,
            Status = ClaimStatus.Submitted,
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
