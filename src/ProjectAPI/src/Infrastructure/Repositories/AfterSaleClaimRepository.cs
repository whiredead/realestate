using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Domain.Sales.Interfaces;
using ProjectAPI.Infrastructure.Context;
using Als.Foundation.Data.EntityFramework;


namespace ProjectAPI.Infrastructure.Repositories;

public class AfterSaleClaimRepository(ApplicationDbContext context) : BaseRepository<AfterSaleClaim>(context), IAfterSaleClaimRepository
{
    private readonly ApplicationDbContext _context = context;

    /// <inheritdoc />
    public async Task<IEnumerable<AfterSaleClaim>> GetAllWithAttachmentsAsync()
    {
        return await _context.AfterSaleClaims
            .Include(c => c.Attachments)
            .ToListAsync();
    }
}
