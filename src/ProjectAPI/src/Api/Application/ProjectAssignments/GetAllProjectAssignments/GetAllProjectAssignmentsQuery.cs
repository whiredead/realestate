using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.ProjectAssignments.GetProjectAssignmentById;

namespace ProjectAPI.Api.Application.ProjectAssignments.GetAllProjectAssignments
{
    /// <summary>
    /// Query to retrieve a paginated list of all project assignments.
    /// </summary>
    public class GetAllProjectAssignmentsQuery : IRequest<PaginatedResponse<GetProjectAssignmentByIdResponse>>
    {
        /// <summary>
        /// Optional filter by Project ID.
        /// </summary>
        public Guid? ProjectId { get; set; }

        /// <summary>
        /// Optional filter by Agent ID.
        /// </summary>
        public string? AgentId { get; set; }
        /// <summary>
        /// Optional filter by Notary ID.
        /// </summary>
        public string? NotaryId { get; set; }

        /// <summary>
        /// Gets or sets the page number (default is 1).
        /// </summary>
        public int PageNumber { get; set; } = 1;

        /// <summary>
        /// Gets or sets the page size (default is 10).
        /// </summary>
        public int PageSize { get; set; } = 10;
    }
}
