using Als.Foundation.Data.Abstractions.EntityFramework;
using ProjectAPI.Domain.Projects.DTOs;
using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Domain.Projects.Interfaces;

public interface IProjectRepository : IBaseRepository<Project>
{
    /// <summary>
    /// One page of projects plus the total matching count. <paramref name="scopeProjectIds"/>
    /// (when not null) restricts the result to those projects BEFORE paging, so
    /// the page and the total are both computed on the caller's perimeter.
    /// </summary>
    Task<(List<ProjectDTO> Items, int TotalCount)> GetProjects(string? UserId, string? Name, string? Location, string Adress, string? Status, IReadOnlyCollection<Guid>? scopeProjectIds, int PageNumber, int PageSize, Guid? Id = null);
    Task<Project?> GetByIdWithTypeBiensAsync(Guid id);
}

