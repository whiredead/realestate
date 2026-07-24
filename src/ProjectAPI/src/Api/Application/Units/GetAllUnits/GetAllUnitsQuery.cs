using ProjectAPI.Api.Application.Common.Models;

namespace ProjectAPI.Api.Application.Units.GetAllUnits;

/// <summary>
/// Query for retrieving all units with optional filters and pagination.
/// </summary>
public class GetAllUnitsQuery : IRequest<PaginatedResponse<UnitResponse>>
{
    /// <summary>
    /// Gets or sets the floor to filter units by.
    /// </summary>
    public string? Floor { get; set; }

    /// <summary>
    /// Gets or sets the minimum number of bedrooms to filter units by.
    /// </summary>
    public int? MinBedrooms { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of bedrooms to filter units by.
    /// </summary>
    public int? MaxBedrooms { get; set; }

    /// <summary>
    /// Gets or sets the minimum surface area to filter units by.
    /// </summary>
    public double? MinSurface { get; set; }

    /// <summary>
    /// Gets or sets the maximum surface area to filter units by.
    /// </summary>
    public double? MaxSurface { get; set; }

    /// <summary>
    /// Gets or sets the minimum price to filter units by.
    /// </summary>
    public decimal? MinPrice { get; set; }

/// <summary>
    /// Gets or sets the maximum price to filter units by.
    /// </summary>
    public decimal? MaxPrice { get; set; }

    /// <summary>
    /// Gets or sets the project ID to filter units by.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the immeuble ID to filter units by.
    /// </summary>
    public Guid? ImmeubleId { get; set; }

    /// <summary>
    /// Gets or sets the current page number for pagination.
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of items per page for pagination.
    /// </summary>
    public int PageSize { get; set; } = 10;
}
