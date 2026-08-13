using Als.Foundation.Data.EntityFramework;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Users.Interfaces;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Infrastructure.Repositories;

public class AgentDateOverrideRepository : BaseRepository<AgentDateOverride>, IAgentDateOverrideRepository
{
    public AgentDateOverrideRepository(ApplicationDbContext context) : base(context)
    {
    }
}
