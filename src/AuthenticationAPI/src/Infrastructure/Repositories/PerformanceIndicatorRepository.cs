using AuthenticationAPI.Domain.ApplicationUser.Entities;
using AuthenticationAPI.Domain.ApplicationUser.Interfaces;
using AuthenticationAPI.Infrastructure.Context;

namespace AuthenticationAPI.Infrastructure.Repositories;

public class PerformanceIndicatorRepository : EfRepositoryBase<PerformanceIndicator>, IPerformanceIndicatorRepository
{
    public PerformanceIndicatorRepository(ApplicationDbContext context) : base(context)
    {
    }
}
