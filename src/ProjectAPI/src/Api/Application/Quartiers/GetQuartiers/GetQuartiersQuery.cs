using ProjectAPI.Api.Application.Common.Models;

namespace ProjectAPI.Api.Application.Quartiers.GetQuartiers;

/// <summary>
/// Query to retrieve a paginated list of quartiers with optional filters.
/// </summary>
public class GetQuartiersQuery : IRequest<PaginatedResponse<QuartierListItem>>
{
    /// <summary>
    /// Optional filter by name (partial or full).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Page number for pagination.
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Page size for pagination.
    /// </summary>
    public int PageSize { get; set; } = 10;
}
