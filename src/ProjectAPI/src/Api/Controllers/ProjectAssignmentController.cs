using ProjectAPI.Api.Application.ProjectAssignments.CreateProjectAssignment;
using ProjectAPI.Api.Application.ProjectAssignments.DeleteProjectAssignment;
using ProjectAPI.Api.Application.ProjectAssignments.GetAllProjectAssignments;
using ProjectAPI.Api.Application.ProjectAssignments.GetProjectAssignmentById;
using ProjectAPI.Api.Application.ProjectAssignments.UnassignProjectAssignment;
using ProjectAPI.Api.Application.ProjectAssignments.UpdateProjectAssignment;

namespace ProjectAPI.Api.Controllers;

/// <summary>
/// Controller for managing project assignments.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProjectAssignmentController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProjectAssignmentController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Creates a new project assignment.
    /// </summary>
    /// <param name="command">The command containing the details for the new project assignment.</param>
    /// <returns>The response containing the unique identifier of the created assignment.</returns>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectAssignmentCommand command)
    {
        var response = await _mediator.Send(command);
        return Ok(response);
    }
    /// <summary>
    /// Deactivate (unassign) an agent or notary from a project.
    /// </summary>
    [HttpDelete("UnassignAgentsNotaire")]
    public async Task<IActionResult> UnassignAgentsNotaire([FromBody] UnassignProjectAssignmentCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result.Success)
            return BadRequest(result.Message);
        return NoContent();
    }
    /// <summary>
    /// Updates an existing project assignment.
    /// </summary>
    /// <param name="command">The command containing updated assignment details.</param>
    /// <returns>The response confirming the update.</returns>
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateProjectAssignmentCommand command)
    {
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    /// <summary>
    /// Deletes a project assignment.
    /// </summary>
    /// <param name="id">The unique identifier of the assignment to delete.</param>
    /// <returns>An IActionResult indicating the outcome.</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var command = new DeleteProjectAssignmentCommand { Id = id };
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    /// <summary>
    /// Retrieves a project assignment by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the assignment.</param>
    /// <returns>The project assignment details.</returns>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var query = new GetProjectAssignmentByIdQuery { Id = id };
        var response = await _mediator.Send(query);
        return Ok(response);
    }

    /// <summary>
    /// Retrieves a paginated list of project assignments with optional filters.
    /// </summary>
    /// <param name="projectId">Optional filter by project ID.</param>
    /// <param name="agentId">Optional filter by agent ID.</param>
    /// <param name="pageNumber">Page number (default is 1).</param>
    /// <param name="pageSize">Page size (default is 10).</param>
    /// <returns>A paginated list of project assignments.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? projectId, [FromQuery] string? agentId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        var query = new GetAllProjectAssignmentsQuery
        {
            ProjectId = projectId,
            AgentId = agentId,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var response = await _mediator.Send(query);
        return Ok(response);
    }
}
