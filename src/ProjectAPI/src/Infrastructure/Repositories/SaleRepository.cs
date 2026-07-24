using Als.Foundation.Data.EntityFramework;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Common.DTOs;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Domain.Sales.Interfaces;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Infrastructure.Repositories;
public class SaleRepository : BaseRepository<Sale>, ISaleRepository
{
    private readonly ApplicationDbContext _ctx;
    public SaleRepository(ApplicationDbContext ctx) : base(ctx) => _ctx = ctx;

    public Task<List<Sale>> GetByBuyerAsync(string buyerId) =>
        _ctx.Set<Sale>()
            .Where(s => s.BuyerId == buyerId)
            .AsNoTracking()
            .ToListAsync();

    public Task<Sale?> GetWithPaymentsAsync(Guid saleId) =>
        _ctx.Set<Sale>()
            .Include(s => s.Payments)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == saleId);

    public async Task<List<SalesMonthlySummaryDto>> GetMonthlySummaryAsync(int year)
    {
        return await _ctx.Set<Sale>()
            .Where(s => s.SaleDate.Year == year)
            .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
            .Select(g => new SalesMonthlySummaryDto
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                CountSales = g.Count(),
                TotalRevenue = g.Sum(x => x.TotalPrice)
            })
            .OrderBy(x => x.Month)
            .ToListAsync();
    }
}
