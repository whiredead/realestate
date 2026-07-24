using Als.Foundation.Data.EntityFramework;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Common.DTOs;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Infrastructure.Repositories;

public class TypeBienRepository : BaseRepository<TypeBien>, ITypeBienRepository
{
    private readonly ApplicationDbContext _context;
    public TypeBienRepository(ApplicationDbContext context) : base(context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));

    }

    public async Task<List<TypeBienListItem>> GetAllWithLinksAsync()
    {
        // Simple query that doesn't rely on ImmeubleTypeBien table
        // Only uses ProjectTypeBien which exists in the database
        return await _context.TypeBiens
            .Include(tb => tb.Projects)
            .Select(tb => new TypeBienListItem
            {
                Id = tb.Id,
                Name = tb.Name,
                Description = tb.Description,
                Image = tb.Image,
                Price = tb.Price,
                NbrChambre = tb.NbrChambre,
                NbrSalleDeBain = tb.NbrSalleDeBain,
                MinSurface = tb.MinSurface,
                MaxSurface = tb.MaxSurface,
                SurfaceRange = tb.MaxSurface != null
                    ? $"de {tb.MinSurface} à {tb.MaxSurface}"
                    : (tb.MinSurface != null ? $"à partir de {tb.MinSurface}" : null),
                ImagesInterieur = string.IsNullOrWhiteSpace(tb.ImagesInterieur)
                    ? new List<string>()
                    : tb.ImagesInterieur!.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),

                // Use ProjectTypeBien instead of ImmeubleTypeBien
                ImmeubleIds = new List<Guid>(), // Not available without ImmeubleTypeBien table
                ProjectIds = tb.Projects
                    .Where(ptb => ptb.ProjectId.HasValue)
                    .Select(ptb => ptb.ProjectId!.Value)
                    .Distinct()
                    .ToList()
            })
            .ToListAsync();
    }
}
