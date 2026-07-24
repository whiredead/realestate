using Als.Foundation.Data.Abstractions.EntityFramework;
using ProjectAPI.Domain.Immeubles.Entities;

namespace ProjectAPI.Domain.Immeubles.Interfaces
{
    /// <summary>
    /// Repository interface for managing <see cref="Immeuble"/> entities.
    /// </summary>
    public interface IImmeubleRepository : IBaseRepository<Immeuble>
    {
        Task<Immeuble?> GetByIdWithDependenciesAsync(Guid id);
    }
}
