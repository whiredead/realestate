using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Domain.Immeubles.Interfaces;

namespace ProjectAPI.Api.Application.Units.GetUnitsByProjectId;

/// <summary>
/// Handler to process retrieving units by project ID with pagination.
/// </summary>
public class GetUnitsByProjectIdHandler : IRequestHandler<GetUnitsByProjectIdQuery, PaginatedResponse<UnitResponse>>
{
    private readonly IUnitRepository _unitRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetUnitsByProjectIdHandler"/> class.
    /// </summary>
    /// <param name="unitRepository">The repository to access unit data.</param>
    public GetUnitsByProjectIdHandler(IUnitRepository unitRepository)
    {
        _unitRepository = unitRepository;
    }

    /// <summary>
    /// Handles the query to retrieve units based on the specified project ID with pagination.
    /// </summary>
    /// <param name="request">The query containing the project ID, page number, and page size.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated response containing the units associated with the specified project ID.</returns>
    public async Task<PaginatedResponse<UnitResponse>> Handle(GetUnitsByProjectIdQuery request, CancellationToken cancellationToken)
    {
        // Retrieve filtered units based on the project ID — Floor and TypeBien
        // are eager-loaded since the projection below reads their display names.
        var matching = (await _unitRepository.Find(
            u => u.ProjectId == request.ImmeubleId, u => u.Floor, u => u.TypeBien)).ToList();
        // The total is the count BEFORE paging; it used to be the size of the
        // returned page, so clients never knew another page existed.
        var totalItems = matching.Count;
        var units = matching
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
                        Status = u.Status.ToCode(),
                        Images = u.Images,
                        TypeBienId = u.TypeBienId,
                        TypeBienName = u.TypeBien?.Name
                    }).ToList();

        // Calculate the total number of items for pagination

        // Return the paginated response
        return new PaginatedResponse<UnitResponse>(units, request.PageNumber, request.PageSize, totalItems);
    }
}
