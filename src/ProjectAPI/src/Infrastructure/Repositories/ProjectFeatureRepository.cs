using Als.Foundation.Data.EntityFramework;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Infrastructure.Repositories;

public class ProjectFeatureRepository : BaseRepository<ProjectFeature>, IProjectFeatureRepository
{
    private readonly ApplicationDbContext _context;
    public ProjectFeatureRepository(ApplicationDbContext context) : base(context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

    }
}
