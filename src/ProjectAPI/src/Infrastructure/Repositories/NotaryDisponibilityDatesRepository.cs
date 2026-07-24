using Als.Foundation.Data.EntityFramework;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Users.Interfaces;
using ProjectAPI.Infrastructure.Context;


namespace ProjectAPI.Infrastructure.Repositories;

/// <summary>
/// Repository class for managing <see cref="NotaryDateDisponibilite"/> entities.
/// </summary>
public class NotaryDisponibilityDatesRepository : BaseRepository<NotaryDateDisponibilite>, INotaryDisponibilityDatesRepository
{
    private readonly ApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotaryDisponibilityDatesRepository"/> class.
    /// </summary>
    /// <param name="context">The database context to be used by this repository.</param>
    /// <exception cref="ArgumentNullException">Thrown when the context is null.</exception>
    public NotaryDisponibilityDatesRepository(ApplicationDbContext context) : base(context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }
}

