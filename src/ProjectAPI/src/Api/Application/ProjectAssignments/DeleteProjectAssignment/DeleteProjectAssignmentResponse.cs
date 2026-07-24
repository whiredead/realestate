namespace ProjectAPI.Api.Application.ProjectAssignments.DeleteProjectAssignment
{
    /// <summary>
    /// Response returned after deleting a project assignment.
    /// </summary>
    public class DeleteProjectAssignmentResponse
    {
        /// <summary>
        /// Gets or sets the unique identifier of the deleted project assignment.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets a success message.
        /// </summary>
        public string Message { get; set; }
    }
}
