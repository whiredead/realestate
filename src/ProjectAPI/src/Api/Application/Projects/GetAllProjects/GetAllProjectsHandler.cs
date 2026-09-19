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

        // §6.3/§6.4 — this endpoint is [AllowAnonymous] because it also serves
        // the public catalogue ("L publié" pour le visiteur), so an anonymous
        // or buyer caller must see everything unfiltered. But the SAME route is
        // reused by the admin console's project list (ProjectsListPage.tsx),
        // and an internal caller below GLOBAL_ADMIN must only ever see
        // projects inside their own ProjectMembership perimeter — otherwise a
        // membership-less PROJECT_ADMIN can list and open every project
        // (confirmed regression: F7).
        var isInternal = _currentUser.IsAuthenticated && RoleCodes.Internal.Any(_currentUser.IsInRole);
        var scope = isInternal ? await _projectScope.GetScopedProjectIdsAsync(cancellationToken) : null;

        var (projects, totalItems) = await _projectRepository.GetProjects(
            request.UserId, request.Name, request.Location, request.Address!, request.Status,
            scope, request.PageNumber, request.PageSize, request.Id);

        if (!isInternal)
        {
            // §6.4 — this route is [AllowAnonymous] because it also feeds the
            // public catalogue, and ProjectResponse carries the project's
            // assigned agents and notaries: full names, e-mail addresses,
            // phone numbers and user ids. Any visitor calling
            // GET /api/Projects was handed the staff directory.
            //
            // The staffing is only ever meaningful to an internal caller, so it
            // is stripped for everyone else rather than the route being closed
            // — closing it would break the public home page, which legitimately
            // lists projects.
            foreach (var project in projects)
            {
                project.AssignedAgents = new();
                project.AssignedNotaries = new();
                project.AgentId = null;
                project.AgentPhoneNumber = null;
                project.NotaryId = null;
                project.NotaryPhoneNumber = null;
            }
        }

        // Already one page (and one total) from the repository — never re-paged here.
        var paginatedData = projects
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
                StatusReferenceCode = project.StatusReferenceCode,
                OverAllProgress = project.OverAllProgress,
                NumberLikes = project.NumberLikes,
                WarrantyMonths = project.WarrantyMonths,
                IsLiked = project.IsLiked,
                QuartierId = project.QuartierId,
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
                    // Null for most types: splitting it unguarded crashed the whole project list (500).
                    ImagesInterieur = string.IsNullOrWhiteSpace(tb.ImagesInterieur) ? new List<string>() : tb.ImagesInterieur.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                    MinSurface = tb.MinSurface,
                    MaxSurface = tb.MaxSurface,
                    SurfaceRange = tb.MaxSurface!= null ? $"de { tb.MinSurface} à {tb.MaxSurface}": $"à partir de {tb.MinSurface}",
                    NbrSalleDeBain = tb.NbrSalleDeBain,
                    NbrDouche = tb.NbrDouche,
                    NbrParking = tb.NbrParking,
                    Module3DLink = tb.Module3DLink
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
