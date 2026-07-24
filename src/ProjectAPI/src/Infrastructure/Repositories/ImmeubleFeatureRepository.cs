using Als.Foundation.Data.EntityFramework;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Infrastructure.Context;


namespace ProjectAPI.Infrastructure.Repositories;

public class ImmeubleFeatureRepository : BaseRepository<ImmeubleFeature>, IImmeubleFeatureRepository
{
    private readonly ApplicationDbContext _context;
    public ImmeubleFeatureRepository(ApplicationDbContext context) : base(context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

    }
}