using Als.Foundation.Data.EntityFramework;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Domain.Sales.Interfaces;
using ProjectAPI.Infrastructure.Context;


namespace ProjectAPI.Infrastructure.Repositories;

public class ClaimHistoryRepository(ApplicationDbContext context) : BaseRepository<ClaimHistory>(context), IClaimHistoryRepository
{
}

