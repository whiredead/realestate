using MediatR;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.TypeBiens.GetTypeBiensByProject
{
    /// <summary>
    /// Handles the request to retrieve all TypeBiens associated with a specific Project.
    /// </summary>
    public class GetTypeBiensByProjectHandler : IRequestHandler<GetTypeBiensByProjectQuery, List<TypeBienListItem>>
    {
        private readonly IProjectRepository _projectRepository;
        private readonly ITypeBienRepository _typeBienRepository;
        /// <summary>
        /// Initializes a new instance of the <see cref="GetTypeBiensByProjectHandler"/> class.
        /// </summary>
        /// <param name="projectRepository">The repository for accessing Project data.</param>
        public GetTypeBiensByProjectHandler(IProjectRepository projectRepository, ITypeBienRepository typeBienRepository)
        {
            _projectRepository = projectRepository;
            _typeBienRepository = typeBienRepository;
        }

        /// <summary>
        /// Handles the query to retrieve TypeBiens by Project ID.
        /// </summary>
        /// <param name="request">The query request containing Project ID.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A list of <see cref="TypeBienListItem"/> associated with the given Project.</returns>
        /// <exception cref="NotFoundException">Thrown when the specified Project does not exist.</exception>
        public async Task<List<TypeBienListItem>> Handle(GetTypeBiensByProjectQuery request, CancellationToken cancellationToken)
        {
            if (request.ProjectId.HasValue)
            {
                var project = await _projectRepository.GetByIdWithTypeBiensAsync(request.ProjectId.Value)
                    ?? throw new NotFoundException($"Project with ID {request.ProjectId} not found.");

                return project.TypeBiens
                    .Where(ptb => ptb.TypeBien != null)
                    .Select(ptb => ptb.TypeBien!)
                    .Select(tb => new TypeBienListItem
                    {
                        Id = tb.Id,
                        Name = tb.Name,
                        Description = tb.Description,
                        Image = tb.Image,
                        Price = tb.Price,
                        MinPrice = tb.MinPrice,
                        MaxPrice = tb.MaxPrice,
                        NbrChambre = tb.NbrChambre,
                        NbrSalleDeBain = tb.NbrSalleDeBain,
                        NbrDouche = tb.NbrDouche,
                        NbrParking = tb.NbrParking,
                        Module3DLink = tb.Module3DLink,
                        MinSurface = tb.MinSurface,
                        MaxSurface = tb.MaxSurface,
                        SurfaceRange = tb.MaxSurface != null ? $"de {tb.MinSurface} à {tb.MaxSurface}" : $"à partir de {tb.MinSurface}",
                        ImagesInterieur = string.IsNullOrWhiteSpace(tb.ImagesInterieur)
                            ? new List<string>()
                            : tb.ImagesInterieur!.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                    })
                    .ToList();
            }
            else
            {
                var res = await _typeBienRepository.GetAllWithLinksAsync(); // domain DTOs
                return res.Select(x => new TypeBienListItem
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    Image = x.Image,
                    Price = x.Price,
                    MinPrice = x.MinPrice,
                    MaxPrice = x.MaxPrice,
                    NbrChambre = x.NbrChambre,
                    NbrSalleDeBain = x.NbrSalleDeBain,
                    NbrDouche = x.NbrDouche,
                    NbrParking = x.NbrParking,
                    Module3DLink = x.Module3DLink,
                    MinSurface = x.MinSurface,
                    MaxSurface = x.MaxSurface,
                    SurfaceRange = x.MaxSurface != null ? $"de {x.MinSurface} à {x.MaxSurface}" : $"à partir de {x.MinSurface}",
                    ImagesInterieur = x.ImagesInterieur
                }).ToList();
            }
        }
    }
}
