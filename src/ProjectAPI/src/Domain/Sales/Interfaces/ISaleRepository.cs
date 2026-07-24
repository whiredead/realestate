using Als.Foundation.Data.Abstractions.EntityFramework;
using ProjectAPI.Domain.Common.DTOs;
using ProjectAPI.Domain.Sales.Entities;

namespace ProjectAPI.Domain.Sales.Interfaces;

public interface ISaleRepository : IBaseRepository<Sale>
{
    Task<List<Sale>> GetByBuyerAsync(string buyerId);
    Task<List<SalesMonthlySummaryDto>> GetMonthlySummaryAsync(int year);
    Task<Sale?> GetWithPaymentsAsync(Guid saleId);
}
