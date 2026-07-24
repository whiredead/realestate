namespace ProjectAPI.Api.Application.ProjectAssignments.CreateProjectAssignment
{
    /// <summary>
    /// Command to create a new project assignment.
    /// </summary>
    public class CreateProjectAssignmentCommand : IRequest<CreateProjectAssignmentResponse>
    {
        /// <summary>
        /// Gets or sets the unique identifier of the project to which an agent is being assigned.
        /// </summary>
        public Guid ProjectId { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the agent being assigned.
        /// </summary>
        public string? AgentId { get; set; }
        /// <summary>
        /// Gets or sets the unique identifier of the agent being assigned.
        /// </summary>
        public string? NotaryId { get; set; }


        /// <summary>
        /// Gets or sets a value indicating whether the assignment is active.
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}
