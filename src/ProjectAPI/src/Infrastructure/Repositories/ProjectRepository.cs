using Als.Foundation.Data.EntityFramework;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Projects.DTOs;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Infrastructure.Repositories;

public class ProjectRepository : BaseRepository<Project>, IProjectRepository
{
    private readonly ApplicationDbContext _context;

    public ProjectRepository(ApplicationDbContext context) : base(context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

    }

    /// <summary>
    /// Phase 1 — agent/notary staffing (the UserId filter, AgentId/NotaryId,
    /// AssignedAgents/AssignedNotaries) is resolved from ProjectMembership,
    /// not the legacy Project.Assignments navigation: ProjectAssignmentController
    /// no longer writes ProjectAssignments at all (see its handlers), so any
    /// staffing created/changed after Phase 1 would be invisible to a query
    /// still walking that navigation.
    /// </summary>
    public async Task<(List<ProjectDTO> Items, int TotalCount)> GetProjects(string? UserId, string? Name, string? Location, string Adress, string? Status, IReadOnlyCollection<Guid>? scopeProjectIds, int PageNumber, int PageSize, Guid? Id = null)
    {
        var likedProjectIds = new List<Guid>();

        if (!string.IsNullOrEmpty(UserId))
        {
            var likedProjects = await _context.LikedProjects.Where(lp => lp.UserId == UserId).ToListAsync();
            likedProjectIds = likedProjects.Select(lp => lp.ProjectId).ToList();
        }

        // The agent filter only needs the caller's own memberships.
        var agentProjectIds = string.IsNullOrEmpty(UserId)
            ? null
            : (await _context.Set<ProjectMembership>()
                .Where(m => m.IsActive && m.RoleCode == RoleCodes.SalesAgent && m.UserId == UserId)
                .Select(m => m.ProjectId)
                .ToListAsync());

        var filtered = _context.Projects
            .Where(p =>
                (!Id.HasValue || p.Id == Id.Value) &&
                (scopeProjectIds == null || scopeProjectIds.Contains(p.Id)) &&
                (agentProjectIds == null || agentProjectIds.Contains(p.Id)) &&
                (string.IsNullOrEmpty(Name) || p.Name.Contains(Name)) &&
                (string.IsNullOrEmpty(Location) || p.Location.Contains(Location)) &&
                (string.IsNullOrEmpty(Adress) || p.Address.Contains(Adress)) &&
                (string.IsNullOrEmpty(Status) || p.StatusGlobal == Status)
            );

        // Counted on the same filter as the page (scope included) — the handler
        // used to filter the scope AFTER this method had already paged, then
        // page a second time, so totals were wrong and page 2 was always empty.
        var totalCount = await filtered.CountAsync();

        var projects = await filtered
            .Include(p => p.TypeBiens)
            .ThenInclude(ptb => ptb.TypeBien)
            .Include(p => p.Quartier)
            .OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Location,
                p.Address,
                p.Description,
                p.Module3DLink,
                p.StatusGlobal,
                p.Type,
                p.OverAllProgress,
                p.NumberLikes,
                p.WarrantyMonths,
                p.QuartierId,
                p.Quartier,
                p.Images,
                p.TypeBiens
            })
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        // Staffing (active SALES_AGENT/NOTARY memberships + display fields) for
        // THIS page only. It used to be loaded for every project in the database
        // on each call, which made the project list slower with every project.
        var pageIds = projects.Select(p => p.Id).ToList();
        var activeStaff = await (
            from m in _context.Set<ProjectMembership>()
            join u in _context.Users on m.UserId equals u.Id
            where m.IsActive && pageIds.Contains(m.ProjectId)
                  && (m.RoleCode == RoleCodes.SalesAgent || m.RoleCode == RoleCodes.Notary)
            select new { m.ProjectId, m.RoleCode, m.UserId, u.FirstName, u.LastName, u.Email, u.PhoneNumber }
        ).ToListAsync();
        var staffByProject = activeStaff
            .GroupBy(x => x.ProjectId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var items = projects.Select(p =>
        {
            staffByProject.TryGetValue(p.Id, out var staff);
            staff ??= new();

            var agents = staff.Where(s => s.RoleCode == RoleCodes.SalesAgent)
                .GroupBy(s => s.UserId)
                .Select(g => g.First())
                .Select(a => new AgentDTO
                {
                    Id = a.UserId,
                    FirstName = a.FirstName,
                    LastName = a.LastName,
                    Email = a.Email!,
                    PhoneNumber = a.PhoneNumber!
                })
                .ToList();

            var notaries = staff.Where(s => s.RoleCode == RoleCodes.Notary)
                .GroupBy(s => s.UserId)
                .Select(g => g.First())
                .Select(n => new NotaryDTO
                {
                    Id = n.UserId,
                    FirstName = n.FirstName,
                    LastName = n.LastName,
                    Email = n.Email!,
                    PhoneNumber = n.PhoneNumber!
                })
                .ToList();

            var firstAgent = staff.FirstOrDefault(s => s.RoleCode == RoleCodes.SalesAgent);
            var firstNotary = staff.FirstOrDefault(s => s.RoleCode == RoleCodes.Notary);

            return new ProjectDTO
            {
                Id = p.Id,
                Name = p.Name,
                Location = p.Location,
                Address = p.Address,
                Description = p.Description,
                Module3DLink = p.Module3DLink,
                StatusGlobal = p.StatusGlobal,
                Type = p.Type,
                OverAllProgress = p.OverAllProgress,
                NumberLikes = p.NumberLikes,
                WarrantyMonths = p.WarrantyMonths,
                IsLiked = likedProjectIds.Contains(p.Id),
                QuartierId = p.QuartierId,
                QuartierName = p.Quartier != null ? p.Quartier.Name : null,
                QuartierDescription = p.Quartier != null ? p.Quartier.Description : null,
                QuartierImages = p.Quartier != null ? p.Quartier.Images : null,
                Images = p.Images,
                AgentId = firstAgent?.UserId,
                AgentPhoneNumber = firstAgent?.PhoneNumber,
                NotaryId = firstNotary?.UserId,
                NotaryPhoneNumber = firstNotary?.PhoneNumber,
                TypeBiens = p.TypeBiens
                    .Where(ptb => ptb.TypeBien != null)
                    .Select(ptb => new TypeBienDTO
                    {
                        Id = ptb.TypeBien!.Id,
                        Name = ptb.TypeBien.Name,
                        Description = ptb.TypeBien.Description,
                        Image = ptb.TypeBien.Image,
                        Price = ptb.TypeBien.Price,
                        NbrChambre = ptb.TypeBien.NbrChambre,
                        NbrSalleDeBain = ptb.TypeBien.NbrSalleDeBain,
                        NbrDouche = ptb.TypeBien.NbrDouche,
                        NbrParking = ptb.TypeBien.NbrParking,
                        Module3DLink = ptb.TypeBien.Module3DLink,
                        MinSurface = ptb.TypeBien.MinSurface,
                        MaxSurface = ptb.TypeBien.MaxSurface,
                        SurfaceRange = ptb.TypeBien.MaxSurface != null ? $"de {ptb.TypeBien.MinSurface} à {ptb.TypeBien.MaxSurface}" : $"à partir de {ptb.TypeBien.MinSurface}",
                        ImagesInterieur = ptb.TypeBien.ImagesInterieur
                    })
                    .ToList(),
                AssignedAgents = agents,
                AssignedNotaries = notaries
            };
        }).ToList();

        return (items, totalCount);
    }

    public async Task<Project?> GetByIdWithTypeBiensAsync(Guid id)
    {
        return await _context.Projects
            .Include(p => p.TypeBiens)
                .ThenInclude(ptb => ptb.TypeBien)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public override async Task<Project?> GetByIDAsync(object id)
    {
        var guidId = (Guid)id;
        return await _context.Projects.FirstOrDefaultAsync(p => p.Id == guidId);
    }
}
