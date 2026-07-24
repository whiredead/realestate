namespace ProjectAPI.Api.Application.ProjectAssignments.GetProjectAssignmentById
{
    /// <summary>
    /// Response model for a project assignment.
    /// </summary>
    public class GetProjectAssignmentByIdResponse
    {
        /// <summary>
        /// Gets or sets the unique identifier of the project assignment.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the related project.
        /// </summary>
        public Guid ProjectId { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the agent.
        /// </summary>
        public string? AgentId { get; set; }
        /// <summary>
        /// Gets or sets the unique identifier of the notary.
        /// </summary>
        public string? NotaryId { get; set; }
        /// <summary>
        /// Gets or sets the creation date of the assignment.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the assignment is active.
        /// </summary>
        public bool IsActive { get; set; }
    }
}
