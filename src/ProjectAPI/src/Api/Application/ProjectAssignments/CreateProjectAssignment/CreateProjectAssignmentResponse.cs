namespace ProjectAPI.Api.Application.ProjectAssignments.CreateProjectAssignment
{
    /// <summary>
    /// Response returned after creating a project assignment.
    /// </summary>
    public class CreateProjectAssignmentResponse
    {
        /// <summary>
        /// Gets or sets the unique identifier of the created project assignment.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets a success message.
        /// </summary>
        public string Message { get; set; }
    }
}
