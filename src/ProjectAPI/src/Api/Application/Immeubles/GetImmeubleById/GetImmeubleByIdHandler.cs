using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.TypeBiens.GetTypeBiensByImmeuble;
using ProjectAPI.Domain.Immeubles.Interfaces;

namespace ProjectAPI.Api.Application.Immeubles.GetImmeubleById;

/// <summary>
/// Handler for the <see cref="GetImmeubleByIdQuery"/>.
/// </summary>
public class GetImmeubleByIdHandler : IRequestHandler<GetImmeubleByIdQuery, ImmeubleResponse>
{
    private readonly IImmeubleRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetImmeubleByIdHandler"/> class.
    /// </summary>
    /// <param name="repository">The project repository.</param>
    public GetImmeubleByIdHandler(IImmeubleRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Handles the request to get a project by its ID.
    /// </summary>
    /// <param name="request">The request containing the project ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The response containing the project details.</returns>
    public async Task<ImmeubleResponse> Handle(GetImmeubleByIdQuery request, CancellationToken cancellationToken)
    {
        // Retrieve the Immeuble, including PlanInterieurs
        var immeubles = await _repository.Find(
            p => p.Id == request.Id,
            p => p.PlanInterieurs
        );

        var immeuble = immeubles.FirstOrDefault();
        if (immeuble == null)
        {
            throw new NotFoundException($"Immeuble with ID {request.Id} not found.");
        }

        // Build the response object
        return new ImmeubleResponse
        {
            Id = immeuble.Id,
            ProjectId = immeuble.ProjectId,
            AgentId = immeuble.AgentId,
            Name = immeuble.Name,
            Location = immeuble.Location,
            Type = immeuble.Type,
            MinPrice = immeuble.MinPrice,
            MaxPrice = immeuble.MaxPrice,

            // Convert the stored string status to an enum (optional approach)
            Status = Enum.Parse<ProjectStatus>(immeuble.Status),

            // If immeuble.Images is a comma-separated string
            // e.g., "img1.jpg,img2.jpg"
            Images = immeuble.Images != null
                ? immeuble.Images.Split(',').ToList()
                : new List<string>(),

            Description = immeuble.Description,
            Latitude = immeuble.Latitude,
            Longitude = immeuble.Longitude,
            NumberOfUnits = immeuble.NumberOfUnits,
            MaxSellableSurfaceRange = immeuble.MaxSellableSurfaceRange,
            MinSellableSurfaceRange = immeuble.MinSellableSurfaceRange,
            Module3DLink = immeuble.Module3DLink,
            NumberOfAvailableUnites = immeuble.NumberOfAvailableUnites,
            NumberOfSoldUnites = immeuble.NumberOfSoldUnites,
            SellsPercentage = immeuble.SellsPercentage,

            // PlanInterieurs sub-collection
            PlanInerieurs = immeuble.PlanInterieurs
                .Select(pi => new PlanInerieurResponse
                {
                    Id = pi.Id,
                    ImmeubleId = pi.ImmeubleId,
                    PhotoLinks = pi.PhotoLinks
                })
                .ToList(),

            // TypeBiens are now associated with Projects, not Immeubles
            TypeBiens = new List<TypeBienListItem>()
        };
    }
}
