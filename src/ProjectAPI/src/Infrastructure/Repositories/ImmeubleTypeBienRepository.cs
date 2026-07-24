using Als.Foundation.Data.EntityFramework;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Infrastructure.Repositories;

public class ImmeubleTypeBienRepository : BaseRepository<ImmeubleTypeBien>, IImmeubleTypeBienRepository
{
    private readonly ApplicationDbContext _context;
    public ImmeubleTypeBienRepository(ApplicationDbContext context) : base(context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

    }
}
