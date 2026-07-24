using ProjectAPI.Domain.Purchases.Interfaces;
using ProjectAPI.Domain.Sales.Interfaces;

namespace ProjectAPI.Api.Application.Sales.GetSalesByUser;

public class GetSalesByUserHandler : IRequestHandler<GetSalesByUserQuery, UserSalesResponse>
{
    private readonly ISaleRepository _saleRepo;
    private readonly IPurchaseRepository _purchaseRepo;

    public GetSalesByUserHandler(ISaleRepository saleRepo, IPurchaseRepository purchaseRepo)
    {
        _saleRepo = saleRepo;
        _purchaseRepo = purchaseRepo;
    }

    public async Task<UserSalesResponse> Handle(GetSalesByUserQuery r, CancellationToken ct)
    {
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
