namespace ProjectAPI.Api.Application.ProjectAssignments.DeleteProjectAssignment
{
    /// <summary>
    /// Command to delete a project assignment.
    /// </summary>
    public class DeleteProjectAssignmentCommand : IRequest<DeleteProjectAssignmentResponse>
    {
        /// <summary>
        /// Gets or sets the unique identifier of the project assignment to delete.
        /// </summary>
        public Guid Id { get; set; }
    }
}
