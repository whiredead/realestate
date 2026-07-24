using Als.Foundation.Data.EntityFramework;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Infrastructure.Context;


namespace ProjectAPI.Infrastructure.Repositories;

public class LikedProjectRepository : BaseRepository<LikedProject>, ILikedProjectRepository
{
    private readonly ApplicationDbContext _context;
    public LikedProjectRepository(ApplicationDbContext context) : base(context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

    }
}
