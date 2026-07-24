using ProjectAPI.Domain.Common.DTOs;
using ProjectAPI.Domain.Common.Interfaces;
using ProjectAPI.Domain.Immeubles.Entities;

namespace ProjectAPI.Domain.Immeubles.Interfaces;

public interface ITypeBienRepository : IBaseRepository<TypeBien>
{
    Task<List<TypeBienListItem>> GetAllWithLinksAsync();
}
