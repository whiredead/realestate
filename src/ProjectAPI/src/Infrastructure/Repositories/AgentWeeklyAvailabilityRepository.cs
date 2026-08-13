using Als.Foundation.Data.EntityFramework;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Users.Interfaces;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Infrastructure.Repositories;

public class AgentWeeklyAvailabilityRepository : BaseRepository<AgentWeeklyAvailability>, IAgentWeeklyAvailabilityRepository
{
    public AgentWeeklyAvailabilityRepository(ApplicationDbContext context) : base(context)
    {
    }
}
