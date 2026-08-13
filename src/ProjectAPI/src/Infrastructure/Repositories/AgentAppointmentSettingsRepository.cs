using Als.Foundation.Data.EntityFramework;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Users.Interfaces;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Infrastructure.Repositories;

public class AgentAppointmentSettingsRepository : BaseRepository<AgentAppointmentSettings>, IAgentAppointmentSettingsRepository
{
    public AgentAppointmentSettingsRepository(ApplicationDbContext context) : base(context)
    {
    }
}
