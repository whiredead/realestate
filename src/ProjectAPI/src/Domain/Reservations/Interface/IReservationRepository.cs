using Als.Foundation.Data.Abstractions.EntityFramework;
using ProjectAPI.Domain.Reservations.Entities;

namespace ProjectAPI.Domain.Reservations.Interface;

public interface IReservationRepository : IBaseRepository<Reservation>
{
    /// <summary>
    /// Gets a reservation by ID with its Documents collection loaded.
    /// </summary>
    Task<Reservation?> GetByIdWithDocumentsAsync(Guid id);
    
    /// <summary>
    /// Adds a document to a reservation directly via DbContext.
    /// </summary>
    Task AddDocumentAsync(ReservationDocument document);
}

