using ProjectAPI.Api.Application.Immeubles.CreateIImmeubleFeature;
using ProjectAPI.Api.Application.Immeubles.CreateImmeuble;
using ProjectAPI.Api.Application.Immeubles.DeleteImmeuble;
using ProjectAPI.Api.Application.Immeubles.GetAllImmeubles;
using ProjectAPI.Api.Application.Immeubles.GetImmeubleById;
using ProjectAPI.Api.Application.Immeubles.GetImmeubleFeatures;
using ProjectAPI.Api.Application.Immeubles.Tracking.AddImmeubleTracking;
using ProjectAPI.Api.Application.Immeubles.Tracking.GetImmeubleTracking;
using ProjectAPI.Api.Application.Immeubles.UpdateImmeuble;
using ProjectAPI.Api.Application.Units.CreateProjectUnit;
using ProjectAPI.Api.Application.Units.GetAllUnits;
using ProjectAPI.Api.Application.Units.GetUnitsByProjectId;
using ProjectAPI.Api.Application.Units.UpdateUnit;
using ProjectAPI.Api.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;

namespace ProjectAPI.Api.Controllers;
/// <summary>
/// Controller for managing immeubles.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize] // Fail closed. Catalogue reads opt out with [AllowAnonymous]; stock writes are admin-only (§6.3).
public class ImmeubleController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmeubleController"/> class.
    /// </summary>
    /// <param name="mediator">The mediator to handle commands and queries.</param>        

    public ImmeubleController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Creates a new immeuble.
    /// </summary>
    /// <param name="command">The command containing the details for the new immeuble.</param>
    /// <returns>The response containing the unique identifier of the created immeuble.</returns>
    [HttpPost]
    [Authorize(Roles = RoleGroups.Admins)] // §6.3 Stock: "A périmètre".
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateImmeuble([FromBody] CreateImmeubleCommand command)
    {

        /*var rolesClaim = User.FindFirst("Roles")?.Value;
        if (rolesClaim != null)
        {
            var roles = rolesClaim.Split(',').Select(r => r.Trim());
            if (!roles.Contains("Admin") && !roles.Contains("Agent"))
            {
                return Forbid();
            }
        }
        else
        {
            return Forbid();
        }*/

        var immeubleId = await _mediator.Send(command);
        var response = new CreateImmeubleResponse
        {
            ProjectId = immeubleId,
            Message = "Immeuble created successfully."
        };

        return Ok(response);
    }

    /// <summary>
    /// Gets all immeubles with optional filters and pagination.
    /// </summary>
    /// <param name="query">The query containing filter and pagination parameters.</param>
    /// <returns>The response containing a paginated list of immeubles.</returns>
    [HttpGet]
    [AllowAnonymous] // §6.3 Catalogue public.
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAllImmeubles([FromQuery] GetAllImmeublesQuery query)
    {
        try
        {
            var response = await _mediator.Send(query);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }

    }

    /// <summary>
    /// Gets a immeuble by its ID.
    /// </summary>
    /// <param name="id">The ID of the immeuble to retrieve.</param>
    /// <returns>The immeuble details.</returns>
    [HttpGet("{id}")]
    [AllowAnonymous] // §6.3 Catalogue public.
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetImmeubleById(Guid id)
    {
        var query = new GetImmeubleByIdQuery(id);
        var response = await _mediator.Send(query);
        return Ok(response);
    }
    /// <summary>
    /// Updates a immeuble by its ID.
    /// </summary>
    /// <param name="id">The ID of the immeuble to update.</param>
    /// <param name="command">The immeuble update details.</param>
    /// <returns>The updated immeuble details.</returns>
    [HttpPut("{id}")]
    [Authorize(Roles = RoleGroups.Admins)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateImmeuble(Guid id, [FromBody] UpdateImmeubleCommand command)
    {
        if (id != command.Id)
        {
            return BadRequest("Immeuble ID in the URL does not match the command ID.");
        }

        var response = await _mediator.Send(command);
        return Ok(response);
    }


    /// <summary>
    /// Deletes a immeuble by its ID.
    /// </summary>
    /// <param name="id">The ID of the immeuble to delete.</param>
    /// <returns>A response indicating the result of the delete operation.</returns>
    [HttpDelete("{id}")]
    [Authorize(Roles = RoleGroups.Admins)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteImmeuble(Guid id)
    {
        var command = new DeleteImmeublesCommand(id);
        var response = await _mediator.Send(command);
        
        if (!response.Success)
            return BadRequest(response);
            
        return Ok(response);
    }

    /// <summary>
    /// Adds a list of units to a immeuble.
    /// </summary>
    /// <param name="immeubleId">The ID of the immeuble to which units will be added.</param>
    /// <param name="command">The command containing the list of units to be added.</param>
    /// <returns>A response indicating the result of the add operation.</returns>
    [HttpPost("{immeubleId}/units")]
    [Authorize(Roles = RoleGroups.Admins)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddUnitsToImmeuble(Guid immeubleId, [FromBody] CreateProjectUnitCommand command)
    {
        command.ProjectId = immeubleId;
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    /// <summary>
    /// Gets units by immeuble ID with pagination.
    /// </summary>
    /// <param name="query">Query parameters including immeuble ID, page number, and page size.</param>
    /// <returns>A paginated list of units associated with the specified immeuble.</returns>
    [HttpGet("by-immeuble")]
    [AllowAnonymous] // §6.3 Catalogue public — biens d'un immeuble.
    public async Task<IActionResult> GetUnitsByImmeubleId([FromQuery] GetUnitsByProjectIdQuery query)
    {
        var response = await _mediator.Send(query);
        return Ok(response);
    }

    /// <summary>
    /// Gets all units with optional filters.
    /// </summary>
    /// <param name="queryParams">Query parameters to filter units.</param>
    /// <returns>A filtered and paginated list of units.</returns>
    [HttpGet("all")]
    [AllowAnonymous] // §6.3 Catalogue public — liste des biens.
    public async Task<IActionResult> GetAllUnits([FromQuery] GetAllUnitsQuery queryParams)
    {
        var response = await _mediator.Send(queryParams);
        return Ok(response);
    }
    [HttpPut("update/{id}")]
    [Authorize(Roles = RoleGroups.Admins)] // §6.3 Stock et prix: "A périmètre".
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateUnit(Guid id, [FromBody] UpdateUnitCommand command)
    {
        if (id != command.Id)
        {
            return BadRequest("Unit ID mismatch.");
        }

        var response = await _mediator.Send(command);
        return response.IsSuccess ? Ok(response.Message) : BadRequest(response.Message);
    }

    /// <summary>
    /// Add features to an Immeuble.
    /// </summary>
    /// <param name="command">The command containing the features to add.</param>
    /// <returns>Success status.</returns>
    [HttpPost("features")]
    [Authorize(Roles = RoleGroups.Admins)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddImmeubleFeatures([FromBody] AddImmeubleFeatureCommand command)
    {
        var result = await _mediator.Send(command);
        return result ? Ok("Features added successfully.") : BadRequest("Failed to add features.");
    }

    /// <summary>
    /// Get all features for a specific Immeuble.
    /// </summary>
    /// <param name="query">The query containing the Immeuble ID.</param>
    /// <returns>List of features.</returns>
    [HttpGet("features")]
    [AllowAnonymous] // §6.3 Catalogue public.
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImmeubleFeatures([FromQuery] GetImmeubleFeaturesQuery query)
    {
        var features = await _mediator.Send(query);
        if (features == null || features.Count == 0)
            return NotFound("No features found for the specified Immeuble.");

        return Ok(features);
    }
    [HttpPost("{immeubleId}/tracking")]
    [Authorize(Roles = RoleGroups.Admins)] // §6.3 Avancement construction: "A périmètre".
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddImmeubleTracking([FromRoute] Guid immeubleId, [FromBody] AddImmeubleTrackingCommand command)
    {
        command.ImmeubleId = immeubleId;
        var result = await _mediator.Send(command);

        if (result)
            return Ok(new { Message = "Immeuble tracking added successfully." });

        return BadRequest(new { Message = "Failed to add immeuble tracking." });
    }

    [HttpGet("{immeubleId}/tracking")]
    [AllowAnonymous] // §6.3 Avancement construction: "L publié" pour le visiteur.
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImmeubleTracking([FromRoute] Guid immeubleId)
    {
        var query = new GetImmeubleTrackingQuery { ImmeubleId = immeubleId };
        var trackings = await _mediator.Send(query);

        if (trackings == null || !trackings.Any())
            return NotFound(new { Message = "No tracking records found for the specified immeuble." });

        return Ok(trackings);
    }
}
