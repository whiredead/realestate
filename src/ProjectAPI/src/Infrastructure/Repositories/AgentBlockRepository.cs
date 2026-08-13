using Als.Foundation.Data.EntityFramework;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Users.Interfaces;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Infrastructure.Repositories;

public class AgentBlockRepository : BaseRepository<AgentBlock>, IAgentBlockRepository
{
    public AgentBlockRepository(ApplicationDbContext context) : base(context)
    {
    }
}
