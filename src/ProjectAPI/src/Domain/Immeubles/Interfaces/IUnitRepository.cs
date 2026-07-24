using Als.Foundation.Data.Abstractions.EntityFramework;
using ProjectAPI.Domain.Immeubles.Entities;

namespace ProjectAPI.Domain.Immeubles.Interfaces;
/// <summary>
/// Repository interface for managing <see cref="Unit"/> entities.
/// </summary>
public interface IUnitRepository : IBaseRepository<Unit>
{
}

