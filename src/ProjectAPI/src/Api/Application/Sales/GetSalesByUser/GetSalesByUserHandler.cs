using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Purchases.Interfaces;
using ProjectAPI.Domain.Sales.Interfaces;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Api.Application.Sales.GetSalesByUser;

/// <summary>
/// §6.4 — "Un acheteur ne peut accéder qu'à ses propres données". UserId was
/// taken from the route with no ownership check at all, so any authenticated
/// caller (including a BUYER) could pass an arbitrary UserId and read another
/// buyer's full sales/payment history. Internal roles are unaffected — staff
/// legitimately look up any buyer's sales from a CRM contact profile.
/// </summary>
public class GetSalesByUserHandler : IRequestHandler<GetSalesByUserQuery, UserSalesResponse>
{
    private readonly ISaleRepository _saleRepo;
    private readonly IPurchaseRepository _purchaseRepo;
    private readonly ICurrentUser _currentUser;

    public GetSalesByUserHandler(ISaleRepository saleRepo, IPurchaseRepository purchaseRepo, ICurrentUser currentUser)
    {
        _saleRepo = saleRepo;
        _purchaseRepo = purchaseRepo;
        _currentUser = currentUser;
    }

    public async Task<UserSalesResponse> Handle(GetSalesByUserQuery r, CancellationToken ct)
    {
        var isInternal = _currentUser.Roles.Any(role => RoleCodes.Internal.Contains(role, StringComparer.Ordinal));
        if (!isInternal && !string.Equals(_currentUser.UserId, r.UserId, StringComparison.Ordinal))
        {
            throw BusinessRuleException.BuyerScopeDenied();
        }

        var sales = await _saleRepo.GetByBuyerAsync(r.UserId);

        // Fetch purchases for these sales
        var saleIds = sales.Select(s => s.Id).ToList();
        var purchases = await _purchaseRepo.Find(p => p.SaleId != null && saleIds.Contains(p.SaleId.Value));
        var map = purchases.ToDictionary(p => p.SaleId!.Value, p => p);

        var items = sales.Select(s =>
        {
            map.TryGetValue(s.Id, out var pur);
            return new SaleItem
            {
                SaleId = s.Id,
                UnitId = s.UnitId,
                SaleDate = s.SaleDate,
                TotalPrice = s.TotalPrice,
                Paid = pur?.PaidAmount ?? 0,
                Remaining = pur?.RemainingAmount ?? s.TotalPrice
            };
        }).ToList();

        return new UserSalesResponse
        {
            Sales = items,
            TotalPrice = items.Sum(x => x.TotalPrice),
            TotalPaid = items.Sum(x => x.Paid),
            TotalRemaining = items.Sum(x => x.Remaining)
        };
    }
}
