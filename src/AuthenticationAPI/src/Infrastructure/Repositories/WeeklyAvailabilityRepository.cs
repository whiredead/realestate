using AuthenticationAPI.Domain.ApplicationUser.Entities;
using AuthenticationAPI.Domain.ApplicationUser.Interfaces;
using AuthenticationAPI.Infrastructure.Context;

namespace AuthenticationAPI.Infrastructure.Repositories;

internal class WeeklyAvailabilityRepository : EfRepositoryBase<WeeklyAvailability>, IWeeklyAvailabilityRepository
{
    public WeeklyAvailabilityRepository(ApplicationDbContext context) : base(context)
    {
    }
}
