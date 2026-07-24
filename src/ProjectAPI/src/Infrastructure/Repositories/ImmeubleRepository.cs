using Als.Foundation.Data.EntityFramework;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Infrastructure.Repositories;

/// <summary>
/// Repository class for managing <see cref="Immeuble"/> entities.
/// </summary>
public class ImmeubleRepository : BaseRepository<Immeuble>, IImmeubleRepository
{
    private readonly ApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmeubleRepository"/> class.
    /// </summary>
    /// <param name="context">The database context to be used by this repository.</param>
    /// <exception cref="ArgumentNullException">Thrown when the context is null.</exception>
    public ImmeubleRepository(ApplicationDbContext context) : base(context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

public async Task<Immeuble?> GetByIdWithDependenciesAsync(Guid id)
    {
        return await _context.Immeubles
            .Include(i => i.Units)
            .Include(i => i.Appointments)
            .Include(i => i.Features)
            // .Include(i => i.TypeBiens) // ImmeubleTypeBien table doesn't exist in DB
            // .Include(i => i.Assignments) // Assignments table doesn't exist in DB
            .Include(i => i.PlanInterieurs)
            .FirstOrDefaultAsync(i => i.Id == id);
    }
}