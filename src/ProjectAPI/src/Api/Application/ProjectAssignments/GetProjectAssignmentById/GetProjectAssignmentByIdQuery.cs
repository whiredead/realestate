namespace ProjectAPI.Api.Application.ProjectAssignments.GetProjectAssignmentById
{
    /// <summary>
    /// Query to retrieve a project assignment by its unique identifier.
    /// </summary>
    public class GetProjectAssignmentByIdQuery : IRequest<GetProjectAssignmentByIdResponse>
    {
        /// <summary>
        /// Gets or sets the unique identifier of the project assignment.
        /// </summary>
        public Guid Id { get; set; }
    }
}
