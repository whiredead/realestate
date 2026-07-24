using Als.Foundation.Data.Abstractions.EntityFramework;
using ProjectAPI.Domain.Projects.DTOs;
using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Domain.Projects.Interfaces;

public interface IProjectRepository : IBaseRepository<Project>
{
    Task<List<ProjectDTO>> GetProjects(string? UserId, string? Name, string? Location, string Adress, string? Status, int PageNumber, int PageSize);
    Task<Project?> GetByIdWithTypeBiensAsync(Guid id);
}

