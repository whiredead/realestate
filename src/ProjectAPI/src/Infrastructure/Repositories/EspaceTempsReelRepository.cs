using Als.Foundation.Data.EntityFramework;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Infrastructure.Context;


namespace ProjectAPI.Infrastructure.Repositories;

public class EspaceTempsReelRepository : BaseRepository<EspaceTempsReel>, IEspaceTempsReelRepository
{
    private readonly ApplicationDbContext _context;
    public EspaceTempsReelRepository(ApplicationDbContext context) : base(context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

    }
}