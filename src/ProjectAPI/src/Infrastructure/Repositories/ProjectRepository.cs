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
    public async Task<List<ProjectDTO>> GetProjects(string? UserId, string? Name, string? Location, string Adress, string? Status, int PageNumber, int PageSize)
    {
        var likedProjectIds = new List<Guid>();

        if (!string.IsNullOrEmpty(UserId))
        {
            var likedProjects = await _context.LikedProjects.Where(lp => lp.UserId == UserId).ToListAsync();
            likedProjectIds = likedProjects.Select(lp => lp.ProjectId).ToList();
        }

        // Active SALES_AGENT/NOTARY memberships, joined to the user's
        // display fields — this replaces Project.Assignments.Agent/.Notary.
        var activeStaffQuery =
            from m in _context.Set<ProjectMembership>()
            join u in _context.Users on m.UserId equals u.Id
            where m.IsActive && (m.RoleCode == RoleCodes.SalesAgent || m.RoleCode == RoleCodes.Notary)
            select new { m.ProjectId, m.RoleCode, m.UserId, u.FirstName, u.LastName, u.Email, u.PhoneNumber };

        var activeStaff = await activeStaffQuery.ToListAsync();
        var staffByProject = activeStaff
            .GroupBy(s => s.ProjectId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var agentProjectIds = string.IsNullOrEmpty(UserId)
            ? null
            : activeStaff.Where(s => s.RoleCode == RoleCodes.SalesAgent && s.UserId == UserId)
                .Select(s => s.ProjectId)
                .ToHashSet();

        var projects = await _context.Projects
            .Include(p => p.TypeBiens)
            .ThenInclude(ptb => ptb.TypeBien)
            .Include(p => p.Quartier)
            .Where(p =>
                (agentProjectIds == null || agentProjectIds.Contains(p.Id)) &&
                (string.IsNullOrEmpty(Name) || p.Name.Contains(Name)) &&
                (string.IsNullOrEmpty(Location) || p.Location.Contains(Location)) &&
                (string.IsNullOrEmpty(Adress) || p.Address.Contains(Adress)) &&
                (string.IsNullOrEmpty(Status) || p.StatusGlobal == Status)
            )
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
                p.Quartier,
                p.Images,
                p.TypeBiens
            })
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        return projects.Select(p =>
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
                IsLiked = likedProjectIds.Contains(p.Id),
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
