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
        // Retrieve filtered units based on the project ID
        var units = (await _unitRepository.Find(u => u.ProjectId == request.ImmeubleId))
                    .Skip((request.PageNumber - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .Select(u => new UnitResponse
                    {
                        Id = u.Id,
                        Floor = u.Floor,
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
                        LatestPrice = u.LatestPrice
                    }).ToList();

        // Calculate the total number of items for pagination
        var totalItems = units.Count;

        // Return the paginated response
        return new PaginatedResponse<UnitResponse>(units, request.PageNumber, request.PageSize, totalItems);
    }
}
