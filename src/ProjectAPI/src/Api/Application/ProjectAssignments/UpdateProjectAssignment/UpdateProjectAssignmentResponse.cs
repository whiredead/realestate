namespace ProjectAPI.Api.Application.ProjectAssignments.UpdateProjectAssignment
{
    /// <summary>
    /// Response returned after updating a project assignment.
    /// </summary>
    public class UpdateProjectAssignmentResponse
    {
        /// <summary>
        /// Gets or sets the unique identifier of the updated project assignment.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets a success message.
        /// </summary>
        public string Message { get; set; }
    }
}
