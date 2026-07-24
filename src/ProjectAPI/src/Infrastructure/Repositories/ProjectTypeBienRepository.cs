using ProjectAPI.Domain.Common.Interfaces;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Projects.Interfaces;
using Als.Foundation.Data.EntityFramework;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Infrastructure.Repositories;

/// <summary>
/// Repository class for managing <see cref="ProjectTypeBien"/> entities.
/// </summary>
public class ProjectTypeBienRepository : BaseRepository<ProjectTypeBien>, IProjectTypeBienRepository
{
    public ProjectTypeBienRepository(ApplicationDbContext context) : base(context)
    {
    }
}