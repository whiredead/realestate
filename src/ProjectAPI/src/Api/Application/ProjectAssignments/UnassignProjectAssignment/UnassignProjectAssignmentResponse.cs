namespace ProjectAPI.Api.Application.ProjectAssignments.UnassignProjectAssignment;

public class UnassignProjectAssignmentResponse
{
    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// A human-readable message.
    /// </summary>
    public string Message { get; set; } = default!;
}
