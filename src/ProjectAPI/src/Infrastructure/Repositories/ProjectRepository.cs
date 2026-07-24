using Als.Foundation.Data.EntityFramework;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Projects.DTOs;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Infrastructure.Repositories;

public class ProjectRepository : BaseRepository<Project>, IProjectRepository
{
    private readonly ApplicationDbContext _context;

    public ProjectRepository(ApplicationDbContext context) : base(context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

    }

    public async Task<List<ProjectDTO>> GetProjects(string? UserId, string? Name, string? Location, string Adress, string? Status, int PageNumber, int PageSize)
    {
        var likedProjectIds = new List<Guid>();

        if (!string.IsNullOrEmpty(UserId))
        {
            var likedProjects = await _context.LikedProjects.Where(lp => lp.UserId == UserId).ToListAsync();
            likedProjectIds = likedProjects.Select(lp => lp.ProjectId).ToList();
        }
        var projects = await _context.Projects
            .Include(p => p.TypeBiens)
            .ThenInclude(ptb => ptb.TypeBien)
            .Include(p => p.Quartier)
            .Include(p => p.Assignments)
                .ThenInclude(a => a.Agent)
            .Include(p => p.Assignments)
                .ThenInclude(a => a.Notary)
.Where(p =>
                (string.IsNullOrEmpty(UserId) || p.Assignments.Any(a => a.AgentId == UserId)) &&
                (string.IsNullOrEmpty(Name) || p.Name.Contains(Name)) &&
                (string.IsNullOrEmpty(Location) || p.Location.Contains(Location)) &&
                (string.IsNullOrEmpty(Adress) || p.Address.Contains(Adress)) &&
                (string.IsNullOrEmpty(Status) || p.StatusGlobal == Status)
            )
            .Select(p => new ProjectDTO
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
                AgentId = p.Assignments.FirstOrDefault(p=>p.AgentId !=null)!.AgentId,
                AgentPhoneNumber = p.Assignments.FirstOrDefault(p => p.AgentId != null)!.Agent.PhoneNumber,
                NotaryId = p.Assignments.FirstOrDefault(p => p.NotaryId != null)!.NotaryId,
                NotaryPhoneNumber = p.Assignments.FirstOrDefault(p => p.NotaryId != null)!.Notary.PhoneNumber,   
                // Get TypeBiens directly from Project
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
                AssignedAgents = p.Assignments.Where(p => p.AgentId != null)
                                    .Select(a => new AgentDTO
                                    {
                                        Id = a.AgentId,
                                        FirstName = a.Agent.FirstName,
                                        LastName = a.Agent.LastName,
                                        Email = a.Agent.Email!,
                                        PhoneNumber = a.Agent.PhoneNumber!
                                        
                                    })
                                    .GroupBy(a => a.Id)
                                    .Select(g => g.First())
                                    .ToList(),
                AssignedNotaries = p.Assignments.Where(p=>p.NotaryId != null)
                                    .Select(a => new NotaryDTO
                                    {
                                        Id = a.NotaryId,
                                        FirstName = a.Notary.FirstName,
                                        LastName = a.Notary.LastName,
                                        Email = a.Notary.Email!,
                                        PhoneNumber = a.Notary.PhoneNumber!
                                    })
                                    .GroupBy(n => n.Id)
                                    .Select(g => g.First())
                                    .ToList()

            })
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

return projects;
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
