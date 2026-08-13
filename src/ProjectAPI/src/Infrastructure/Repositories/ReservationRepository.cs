using Als.Foundation.Data.EntityFramework;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Reservations.Interface;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Infrastructure.Repositories;

public class ReservationRepository : BaseRepository<Reservation>, IReservationRepository
{
    private readonly ApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReservationRepository"/> class.
    /// </summary>
    /// <param name="context">The database context to be used by this repository.</param>
    /// <exception cref="ArgumentNullException">Thrown when the context is null.</exception>
    public ReservationRepository(ApplicationDbContext context) : base(context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task<Reservation?> GetByIdWithDocumentsAsync(Guid id)
    {
        return await _context.Reservations
            .Include(r => r.Documents)
            .Include(r => r.Buyers).ThenInclude(b => b.CrmContact)
            .FirstOrDefaultAsync(r => r.Id == id);
    }
    
    /// <inheritdoc />
    public async Task AddDocumentAsync(ReservationDocument document)
    {
        await _context.ReservationDocuments.AddAsync(document);
    }
}
