using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.TypeBiens.GetTypeBiensByImmeuble;
using ProjectAPI.Domain.Projects.DTOs;
using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Api.Application.Projects.GetAllProjects;

public class GetAllProjectsHandler : IRequestHandler<GetAllProjectsQuery, PaginatedResponse<ProjectResponse>>
{
    private readonly IProjectRepository _projectRepository;
    private readonly ILikedProjectRepository _likedProjectRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ProjectScopeService _projectScope;

    public GetAllProjectsHandler(
        IProjectRepository projectRepository,
        ILikedProjectRepository likedProjectRepository,
        ICurrentUser currentUser,
        ProjectScopeService projectScope)
    {
        _projectRepository = projectRepository;
        _likedProjectRepository = likedProjectRepository;
        _currentUser = currentUser;
        _projectScope = projectScope;
    }

    public async Task<PaginatedResponse<ProjectResponse>> Handle(GetAllProjectsQuery request, CancellationToken cancellationToken)
    {
        var projects = await _projectRepository.GetProjects(request.UserId, request.Name, request.Location, request.Address!, request.Status, request.PageNumber, request.PageSize);

        // §6.3/§6.4 — this endpoint is [AllowAnonymous] because it also serves
        // the public catalogue ("L publié" pour le visiteur), so an anonymous
        // or buyer caller must see everything unfiltered. But the SAME route is
        // reused by the admin console's project list (ProjectsListPage.tsx),
        // and an internal caller below GLOBAL_ADMIN must only ever see
        // projects inside their own ProjectMembership perimeter — otherwise a
        // membership-less PROJECT_ADMIN can list and open every project
        // (confirmed regression: F7).
        var isInternal = _currentUser.IsAuthenticated && RoleCodes.Internal.Any(_currentUser.IsInRole);
        if (isInternal)
        {
            var scope = await _projectScope.GetScopedProjectIdsAsync(cancellationToken);
            if (scope is not null)
            {
                projects = projects.Where(p => scope.Contains(p.Id)).ToList();
            }
        }

        var totalItems = projects.Count;

        var paginatedData = projects
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(project => new ProjectResponse
            {
                Id = project.Id,
                Name = project.Name,
                Location = project.Location,
                Address = project.Address,
                Images = project.Images,
                Description = project.Description,
                Module3DLink = project.Module3DLink,
                Type = project.Type,
                StatusGlobal = project.StatusGlobal,
                OverAllProgress = project.OverAllProgress,
                NumberLikes = project.NumberLikes,
                IsLiked = project.IsLiked,
                QuartierName = project.QuartierName,
                QuartierDescription = project.QuartierDescription,
                QuartierImages = project.QuartierImages,
                TypeBiens = [.. project.TypeBiens.Select(tb => new TypeBienListItem
                {
                    Id = tb.Id,
                    Name = tb.Name,
                    Description = tb.Description,
                    Image = tb.Image,
                    NbrChambre = tb.NbrChambre,
                    ImagesInterieur = tb.ImagesInterieur!.Split(',').ToList(),
                    MinSurface = tb.MinSurface,
                    MaxSurface = tb.MaxSurface,
                    SurfaceRange = tb.MaxSurface!= null ? $"de { tb.MinSurface} à {tb.MaxSurface}": $"à partir de {tb.MinSurface}",
                    NbrSalleDeBain =tb.NbrSalleDeBain
                    
                })],
                AgentId = project.AgentId,
                AgentPhoneNumber = project.AgentPhoneNumber,
                AssignedAgents = project.AssignedAgents
                                    .Select(a => new AgentDTO
                                    {
                                        Id = a.Id,
                                        FirstName = a.FirstName,
                                        LastName = a.LastName,
                                        Email = a.Email,
                                        PhoneNumber = a.PhoneNumber
                                    })
                                    .GroupBy(a => a.Id)
                                    .Select(g => g.First())
                                    .ToList(),
                AssignedNotaries = project.AssignedNotaries
                                    .Select(n => new NotaryDTO
                                    {
                                        Id = n.Id,
                                        FirstName = n.FirstName,
                                        LastName = n.LastName,
                                        Email = n.Email,
                                        PhoneNumber = n.PhoneNumber
                                    })
                                    .GroupBy(n => n.Id)
                                    .Select(g => g.First())
                                    .ToList(),
            }).ToList();

        return new PaginatedResponse<ProjectResponse>(paginatedData, request.PageNumber, request.PageSize, totalItems);
    }
}