using Als.Foundation.Data.Abstractions.EntityFramework;
using ProjectAPI.Domain.Sales.Entities;

namespace ProjectAPI.Domain.Sales.Interfaces;

public interface IAfterSaleClaimRepository : IBaseRepository<AfterSaleClaim>
{
    /// <summary>
    /// Gets all claims with their Attachments collection loaded.
    /// </summary>
    Task<IEnumerable<AfterSaleClaim>> GetAllWithAttachmentsAsync();
}
