using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.TypeBiens.GetTypeBiensByImmeuble;

/// <summary>
/// Handles the request to retrieve all TypeBiens associated with a specific Immeuble.
/// </summary>
public class GetTypeBiensByImmeubleHandler : IRequestHandler<GetTypeBiensByImmeubleQuery, List<TypeBienListItem>>
{
    private readonly IImmeubleRepository _immeubleRepository;
    private readonly ITypeBienRepository _typeBienRepository;
    private readonly ApplicationDbContext _context;
    /// <summary>
    /// Initializes a new instance of the <see cref="GetTypeBiensByImmeubleHandler"/> class.
    /// </summary>
    /// <param name="immeubleRepository">The repository for accessing Immeuble data.</param>
    public GetTypeBiensByImmeubleHandler(IImmeubleRepository immeubleRepository, ITypeBienRepository typeBienRepository, ApplicationDbContext context)
    {
        _immeubleRepository = immeubleRepository;
        _typeBienRepository = typeBienRepository;
        _context = context;
    }

    /// <summary>
    /// Handles the query to retrieve TypeBiens by Immeuble ID.
    /// </summary>
    /// <param name="request">The query containing the Immeuble ID.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list of <see cref="TypeBienListItem"/> associated with the given Immeuble.</returns>
    /// <exception cref="NotFoundException">Thrown when the specified Immeuble does not exist.</exception>
    public async Task<List<TypeBienListItem>> Handle(GetTypeBiensByImmeubleQuery request, CancellationToken cancellationToken)
    {
        if (request.ImmeubleId != null)
        {
            var immeuble = await _context.Immeubles
                .Include(i => i.TypeBiens)
                .ThenInclude(itb => itb.TypeBien)
                .FirstOrDefaultAsync(i => i.Id == request.ImmeubleId.Value)
                ?? throw new NotFoundException($"Immeuble with ID {request.ImmeubleId} not found.");

            var projectId = immeuble.ProjectId;

            return immeuble.TypeBiens
                .Where(itb => itb.TypeBien != null)
                .Select(itb => itb.TypeBien!)
                .Select(tb => new TypeBienListItem
                {
                    Id = tb.Id,
                    Name = tb.Name,
                    Description = tb.Description,
                    Image = tb.Image,
                    Price = tb.Price,
                    NbrChambre = tb.NbrChambre,
                    NbrSalleDeBain = tb.NbrSalleDeBain,
                    NbrDouche = tb.NbrDouche,
                    NbrParking = tb.NbrParking,
                    Module3DLink = tb.Module3DLink,
                    MinSurface = tb.MinSurface,
                    MaxSurface = tb.MaxSurface,
                    SurfaceRange = tb.MaxSurface != null
                        ? $"de {tb.MinSurface} à {tb.MaxSurface}"
                        : (tb.MinSurface != null ? $"à partir de {tb.MinSurface}" : null),
                    ImagesInterieur = string.IsNullOrWhiteSpace(tb.ImagesInterieur)
                        ? new List<string>()
                        : tb.ImagesInterieur!.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                    // when filtering by one immeuble, return single ids
                    ImmeubleIds = new List<Guid> { immeuble.Id },
                    ProjectIds = new List<Guid> { projectId }
                })
                .ToList();
        }
        else
        {
            // Global list WITH linked ImmeubleIds and ProjectIds
            var res = await _typeBienRepository.GetAllWithLinksAsync(); // domain DTOs
            return res.Select(x => new TypeBienListItem
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                Image = x.Image,
                Price = x.Price,
                NbrChambre = x.NbrChambre,
                NbrSalleDeBain = x.NbrSalleDeBain,
                NbrDouche = x.NbrDouche,
                NbrParking = x.NbrParking,
                Module3DLink = x.Module3DLink,
                MinSurface = x.MinSurface,
                MaxSurface = x.MaxSurface,
                SurfaceRange = x.SurfaceRange,
                ImagesInterieur = x.ImagesInterieur,
                ImmeubleIds = x.ImmeubleIds,
                ProjectIds = x.ProjectIds
            }).ToList();
        }
    }
}
