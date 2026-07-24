using ProjectAPI.Api.Application.Common.Models;

namespace ProjectAPI.Api.Application.Immeubles.GetAllImmeubles;

/// <summary>
/// Response model for getting all projects.
/// </summary>
public class GetAllImmeublesResponse : PaginatedResponse<ImmeubleResponse>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetAllImmeublesResponse"/> class.
    /// </summary>
    /// <param name="data">The list of project data.</param>
    /// <param name="pageNumber">The current page number.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="totalItems">The total number of items.</param>
    public GetAllImmeublesResponse(IEnumerable<ImmeubleResponse> data, int pageNumber, int pageSize, long totalItems)
        : base(data, pageNumber, pageSize, totalItems)
    {
    }
}
