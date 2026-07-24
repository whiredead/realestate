using Als.Foundation.Data.EntityFramework;
using ProjectAPI.Domain.Purchases.Entities;
using ProjectAPI.Domain.Purchases.Interfaces;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Infrastructure.Repositories;

public class PurchaseRepository : BaseRepository<Purchase>, IPurchaseRepository
{
    private readonly ApplicationDbContext _context;

    public PurchaseRepository(ApplicationDbContext context) : base(context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

    }
}
