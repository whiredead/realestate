namespace ProjectAPI.Api.Application.ProjectAssignments.UpdateProjectAssignment
{
    /// <summary>
    /// Command to update an existing project assignment.
    /// </summary>
    public class UpdateProjectAssignmentCommand : IRequest<UpdateProjectAssignmentResponse>
    {
        /// <summary>
        /// Gets or sets the unique identifier of the project assignment to update.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the updated agent identifier.
        /// </summary>
        public string? AgentId { get; set; }

        /// <summary>
        /// Gets or sets the updated notaire identifier.
        /// </summary>
        public string? NotaryId { get; set; }

        /// <summary>
        /// Gets or sets the updated project identifier.
        /// </summary>
        public Guid? ProjectId { get; set; }

        /// <summary>
        /// Gets or sets the active status.
        /// </summary>
        public bool? IsActive { get; set; }
    }
}
