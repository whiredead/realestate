using Als.Foundation.Data.EntityFramework;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Infrastructure.Repositories;

public class ProjectAgentAssignmentConfigRepository : BaseRepository<ProjectAgentAssignmentConfig>, IProjectAgentAssignmentConfigRepository
{
    public ProjectAgentAssignmentConfigRepository(ApplicationDbContext context) : base(context)
    {
    }
}
