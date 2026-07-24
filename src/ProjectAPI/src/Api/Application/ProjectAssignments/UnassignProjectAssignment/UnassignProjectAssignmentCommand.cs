namespace ProjectAPI.Api.Application.ProjectAssignments.UnassignProjectAssignment;

public class UnassignProjectAssignmentCommand : IRequest<UnassignProjectAssignmentResponse>
{
    /// <summary>
    /// The ID of the project‐assignment to deactivate.
    /// </summary>
    public Guid AssignmentId { get; set; }
}
