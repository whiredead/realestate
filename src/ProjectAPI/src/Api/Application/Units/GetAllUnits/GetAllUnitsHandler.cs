using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Domain.Immeubles.Interfaces;
using System.Linq.Expressions;
using Unit = ProjectAPI.Domain.Immeubles.Entities.Unit;

namespace ProjectAPI.Api.Application.Units.GetAllUnits;

/// <summary>
/// Handler to retrieve all units with optional filters and pagination.
/// </summary>
public class GetAllUnitsHandler : IRequestHandler<GetAllUnitsQuery, PaginatedResponse<UnitResponse>>
{
    private readonly IUnitRepository _unitRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetAllUnitsHandler"/> class.
    /// </summary>
    /// <param name="unitRepository">The repository to access unit data.</param>
    public GetAllUnitsHandler(IUnitRepository unitRepository)
    {
        _unitRepository = unitRepository;
    }

    /// <summary>
    /// Handles the query to retrieve all units based on the specified filters and pagination parameters.
    /// </summary>
    /// <param name="request">The query containing filter and pagination parameters.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated response containing the filtered units.</returns>
public async Task<PaginatedResponse<UnitResponse>> Handle(GetAllUnitsQuery request, CancellationToken cancellationToken)
    {
        // Build dynamic predicate for filtering units
        // Note: Unit.ProjectId is the FK to Immeuble (confusing naming in the domain model)
        // To filter by actual Project, we must go through unit.Immeuble.ProjectId
        Expression<Func<Unit, bool>> predicate = unit =>
            (string.IsNullOrEmpty(request.Floor) || unit.Floor.Name.Contains(request.Floor)) &&
            (!request.MinBedrooms.HasValue || unit.NumberOfBedrooms >= request.MinBedrooms) &&
            (!request.MaxBedrooms.HasValue || unit.NumberOfBedrooms <= request.MaxBedrooms) &&
            (!request.MinSurface.HasValue || unit.ApartmentSurface >= request.MinSurface) &&
            (!request.MaxSurface.HasValue || unit.ApartmentSurface <= request.MaxSurface) &&
            (!request.MinPrice.HasValue || unit.LatestPrice >= request.MinPrice) &&
            (!request.MaxPrice.HasValue || unit.LatestPrice <= request.MaxPrice) &&
            (!request.ProjectId.HasValue || unit.Immeuble.ProjectId == request.ProjectId) &&
            (!request.ImmeubleId.HasValue || unit.ProjectId == request.ImmeubleId);

        // Fetch all filtered units from the repository first — Floor and
        // TypeBien are eager-loaded since the projection below reads their
        // display names.
        var allMatchingUnits = (await _unitRepository.Find(predicate, u => u.Floor, u => u.TypeBien)).ToList();
        
        // Calculate the total number of items BEFORE pagination
        var totalItems = allMatchingUnits.Count;

        // Apply pagination and projection
        var units = allMatchingUnits
            // Stable order before paging: without it page contents are
            // nondeterministic and rows repeat or vanish between pages.
            .OrderBy(u => u.UnitNumber).ThenBy(u => u.Id)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new UnitResponse
            {
                Id = u.Id,
                FloorId = u.FloorId,
                Floor = u.Floor.Name,
                UnitNumber = u.UnitNumber,
                NumberOfBedrooms = u.NumberOfBedrooms,
                NumberOfBathrooms = u.NumberOfBathrooms,
                ApartmentSurface = u.ApartmentSurface,
                BalconySurface = u.BalconySurface,
                TerraceSurface = u.TerraceSurface,
                GardenSurface = u.GardenSurface,
                View = u.View,
                Orientation = u.Orientation,
                TotalSurface = u.TotalSurface,
                SaleableValue = u.SaleableValue,
                SaleableValue1 = u.SaleableValue1,
                PriceSaleableValue = u.PriceSaleableValue,
                PriceSaleableValue1 = u.PriceSaleableValue1,
                LatestPrice = u.LatestPrice,
                // Canonical §3 code, not the enum's numeric value: clients gate on
                // "AVAILABLE", and an integer on the wire would be meaningless.
                Status = u.Status.ToCode(),
                Images = u.Images,
                TypeBienId = u.TypeBienId,
                TypeBienName = u.TypeBien?.Name
            }).ToList();

        // Return the paginated response
        return new PaginatedResponse<UnitResponse>(units, request.PageNumber, request.PageSize, totalItems);
    }
}
