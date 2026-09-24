using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.TypeBiens.GetTypeBiensByImmeuble;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Immeubles.Entities;
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
        // Retrieve the Immeuble, including PlanInterieurs and Units (the
        // latter to compute live stock figures — see the comment below).
        var immeubles = await _repository.Find(
            p => p.Id == request.Id,
            p => p.PlanInterieurs,
            p => p.Units
        );

        var immeuble = immeubles.FirstOrDefault();
        if (immeuble == null)
        {
            throw new NotFoundException($"Immeuble with ID {request.Id} not found.");
        }

        // §3/§7 — NumberOfAvailableUnites/NumberOfSoldUnites are legacy
        // denormalized counters nothing in the codebase ever writes to after
        // creation, so they read as permanently stale (frequently 0, never
        // reflecting a later sale). Unit.Status is the one continuously-
        // maintained source of truth (UnitStatusService), so stock figures
        // are computed live from it instead — see GetAllImmeublesHandler for
        // the matching fix on the list endpoint.
        var totalUnits = immeuble.Units.Count;
        var availableUnits = immeuble.Units.Count(u => u.Status == UnitCommercialStatus.Available);
        var soldUnits = immeuble.Units.Count(u =>
            u.Status == UnitCommercialStatus.Sold || u.Status == UnitCommercialStatus.Delivered);
        // Same in-between states as the list endpoint, so a building's detail
        // page and its row in the inventory table can't disagree.
        var reservedUnits = immeuble.Units.Count(u =>
            u.Status == UnitCommercialStatus.HoldPendingApproval ||
            u.Status == UnitCommercialStatus.Reserved ||
            u.Status == UnitCommercialStatus.Contracted);

        // Build the response object
        return new ImmeubleResponse
        {
            Id = immeuble.Id,
            ProjectId = immeuble.ProjectId,
            AgentId = immeuble.AgentId,
            Name = immeuble.Name,
            Location = immeuble.Location,
            Type = immeuble.Type,

            Status = ImmeubleStatusCodes.Normalize(immeuble.Status),

            // If immeuble.Images is a comma-separated string
            // e.g., "img1.jpg,img2.jpg"
            Images = immeuble.Images != null
                ? immeuble.Images.Split(',').ToList()
                : new List<string>(),

            Description = immeuble.Description,
            NumberOfUnits = totalUnits,
            MaxSellableSurfaceRange = immeuble.MaxSellableSurfaceRange,
            MinSellableSurfaceRange = immeuble.MinSellableSurfaceRange,
            Module3DLink = immeuble.Module3DLink,
            NumberOfAvailableUnites = availableUnits,
            NumberOfSoldUnites = soldUnits,
            NumberOfReservedUnites = reservedUnits,
            SellsPercentage = totalUnits > 0 ? (int)Math.Round(soldUnits * 100.0 / totalUnits) : 0,

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
