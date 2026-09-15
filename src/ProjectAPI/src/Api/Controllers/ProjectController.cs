using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.EspacesTempsReel.CreateEspaceTempsReel;
using ProjectAPI.Api.Application.EspacesTempsReel.GetEspaceTempsReelById;
using ProjectAPI.Api.Application.Projects.AddProjectFratures;
using ProjectAPI.Api.Application.Projects.CreateProjects;
using ProjectAPI.Api.Application.Projects.GetAllProjects;
using ProjectAPI.Api.Application.Projects.GetProjectById;
using ProjectAPI.Api.Application.Projects.GetProjectFeatures;
using ProjectAPI.Api.Application.Projects.LikedProjects.AddLikedProject;
using ProjectAPI.Api.Application.Projects.LikedProjects.GetLikedProjects;
using ProjectAPI.Api.Application.Projects.LikedProjects.RemoveLikedProject;
using ProjectAPI.Api.Application.Projects.LikedProjects.UpdateLikedProject;
using ProjectAPI.Api.Application.Projects.RemoveProject;
using ProjectAPI.Api.Application.Projects.RemoveProjectFeatures;
using ProjectAPI.Api.Application.Projects.UpdateProjects;
using ProjectAPI.Api.Application.Purchases.GetUserPurchases;
using ProjectAPI.Api.Application.Quartiers.CreateQuartier;
using ProjectAPI.Api.Application.Quartiers.GetQuartierById;
using ProjectAPI.Api.Application.Quartiers.GetQuartiers;
using ProjectAPI.Api.Application.TypeBiens.AssociateToProject;
using ProjectAPI.Api.Application.TypeBiens.GetTypeBiensByProject;
using ProjectAPI.Api.Application.Projects.GetQuartierAmenities;
using ProjectAPI.Api.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;

namespace ProjectAPI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Fail closed. Public catalogue reads opt out with [AllowAnonymous]; writes are admin-only (§6.3).
public class ProjectsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUser _currentUser;

    public ProjectsController(IMediator mediator, ICurrentUser currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>A caller holding no internal role (buyer, prospect) may only act on their own favourites.</summary>
    private bool IsInternalCaller => _currentUser.Roles.Any(r => ProjectAPI.Domain.Users.Entities.RoleCodes.Internal.Contains(r, StringComparer.Ordinal));

    [HttpPost]
    [Authorize(Roles = RoleGroups.Admins)] // §6.3 Catalogue: "A périmètre" — création réservée aux admins.
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpGet]
    [AllowAnonymous] // §6.3 Catalogue public: "L" pour le visiteur.
    public async Task<IActionResult> GetAllProjects([FromQuery] GetAllProjectsQuery query)
    {
        /*var idClaims = User.FindFirst("userId")?.Value;
        query.UserId = idClaims;*/
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Drill-down entrypoint: a single project with its immeubles (buildings)
    /// and live per-building stats. No such single-project route existed
    /// before — only the paginated list above.
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = RoleGroups.AdminsAgents)] // §6.4 — project-scoped, not catalogue-public: immeuble stock figures are internal reporting, not §7.2 published data.
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProjectById(Guid id)
    {
        var result = await _mediator.Send(new GetProjectByIdQuery { Id = id });
        return Ok(result);
    }
    /// <summary>
    /// Updates an existing project by its ID.
    /// </summary>
    /// <param name="id">The ID of the project to update.</param>
    /// <param name="command">The project update details.</param>
    /// <returns>The updated project data.</returns>
    [HttpPut("{id}")]
    [Authorize(Roles = RoleGroups.Admins)]
    public async Task<ActionResult<ProjectResponse>> UpdateProject(Guid id, [FromBody] UpdateProjectCommand command)
    {
        if (id != command.Id)
            return BadRequest("Project ID in the route does not match the body.");

        var result = await _mediator.Send(command);
        return Ok(result);
    }
    [HttpPost("Like")]
    public async Task<IActionResult> AddLikedProject([FromBody] AddLikedProjectCommand command)
    {
        // The user comes from the token for a buyer/prospect, never from the body.
        if (!IsInternalCaller) command.UserId = _currentUser.UserId!;
        var response = await _mediator.Send(command);
        return Ok(response);
    }
    /// <summary>
    /// Remove user’s like from a project.
    /// DELETE /api/projects/{projectId}/like?userId=...
    /// </summary>
    [HttpDelete("DisLikeProject")]
    public async Task<IActionResult> DisLikeProject([FromBody] RemoveLikedProjectCommand command)
    {
        if (!IsInternalCaller) command.UserId = _currentUser.UserId!;
        var res = await _mediator.Send(command);
        if (!res.Success) return BadRequest(res.Message);
        return NoContent();
    }
    /// <summary>The signed-in user's own favourites.</summary>
    [HttpGet("LikedProjects/mine")]
    public async Task<IActionResult> GetMyLikedProjects([FromQuery] GetLikedProjectsQuery query)
    {
        query.UserId = _currentUser.UserId;
        return Ok(await _mediator.Send(query));
    }

    /// <summary>
    /// Favourites by user. Any signed-in account could list every user's
    /// favourites (names included) by passing any UserId — or none. A caller
    /// without an internal role is now pinned to their own.
    /// </summary>
    [HttpGet("LikedProjects")]
    public async Task<IActionResult> GetLikedProjects([FromQuery] GetLikedProjectsQuery query )
    {
        if (!IsInternalCaller) query.UserId = _currentUser.UserId;
        var response = await _mediator.Send(query);
        return Ok(response);
    }

    [HttpPut("LikedProject")]
    public async Task<IActionResult> UpdateLikedProject([FromBody] UpdateLikedProjectCommand command)
    {
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    /// <summary>
    /// Add features to a Project.
    /// </summary>
    /// <param name="command">The command containing the features to add.</param>
    /// <returns>Success status.</returns>
    [HttpPost("features")]
    [Authorize(Roles = RoleGroups.Admins)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddProjectFeatures([FromBody] AddProjectFeatureCommand command)
    {
        var result = await _mediator.Send(command);
        // N25/N30 — a bare Ok("...") string throws on the client's JSON.parse
        // of a genuinely successful response; every write endpoint on this
        // controller returns a real object, so this one now does too instead
        // of needing a special-cased text-only fetch for just two endpoints.
        return result
            ? Ok(new { message = "Features added successfully." })
            : BadRequest(new { message = "Failed to add features." });
    }
    /// <summary>
    /// Remove one or more features from a project.
    /// DELETE /api/projects/{projectId}/features
    /// Body: [ "featureId1", "featureId2", … ]
    /// </summary>
    [HttpDelete("RemoveFeatures")]
    [Authorize(Roles = RoleGroups.Admins)]
    public async Task<IActionResult> RemoveFeatures([FromBody] RemoveProjectFeatureCommand command)
    {
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>Edits one project "atout" (name, icon, one-line description).</summary>
    [HttpPut("features/{id:guid}")]
    [Authorize(Roles = RoleGroups.Admins)]
    public async Task<IActionResult> UpdateProjectFeature(Guid id, [FromBody] Application.Projects.UpdateProjectFeature.UpdateProjectFeatureCommand command)
    {
        command.Id = id;
        return Ok(await _mediator.Send(command));
    }

    /// <summary>Removes one project "atout".</summary>
    [HttpDelete("features/{id:guid}")]
    [Authorize(Roles = RoleGroups.Admins)]
    public async Task<IActionResult> DeleteProjectFeature(Guid id)
    {
        await _mediator.Send(new Application.Projects.DeleteProjectFeature.DeleteProjectFeatureCommand { Id = id });
        return NoContent();
    }
    /// <summary>
    /// Get all features for a specific Project.
    /// </summary>
    /// <param name="query">The query containing the Project ID.</param>
    /// <returns>List of features.</returns>
    /// <summary>
    /// Neighbourhood amenities for a project's "Quartier" tab (§ reference site).
    /// </summary>
    [HttpGet("quartier-amenities")]
    [AllowAnonymous] // §6.3 Catalogue public.
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQuartierAmenities([FromQuery] GetQuartierAmenitiesQuery query)
    {
        return Ok(await _mediator.Send(query));
    }

    /// <summary>
    /// §12.1 — documents this project requires on a reservation before it may
    /// be submitted. An empty list means nothing is required, which is every
    /// project until an administrator configures one.
    /// </summary>
    [HttpGet("{projectId:guid}/document-requirements")]
    [Authorize(Roles = RoleGroups.AdminsAgents)] // The agent filling the dossier needs to see what it demands.
    [ProducesResponseType(typeof(List<Application.Projects.DocumentRequirements.ProjectDocumentRequirementResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDocumentRequirements(Guid projectId)
    {
        return Ok(await _mediator.Send(
            new Application.Projects.DocumentRequirements.GetProjectDocumentRequirementsQuery { ProjectId = projectId }));
    }

    /// <summary>
    /// Adds or updates one document requirement (§12.1). Re-sending an existing
    /// DocumentType updates that row instead of failing on the unique index.
    /// </summary>
    [HttpPut("{projectId:guid}/document-requirements")]
    [Authorize(Roles = RoleGroups.Admins)]
    [ProducesResponseType(typeof(Application.Projects.DocumentRequirements.ProjectDocumentRequirementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpsertDocumentRequirement(
        Guid projectId,
        [FromBody] Application.Projects.DocumentRequirements.UpsertProjectDocumentRequirementCommand command)
    {
        command.ProjectId = projectId;
        return Ok(await _mediator.Send(command));
    }

    /// <summary>Removes a document requirement (§12.1 configuration, not business data).</summary>
    [HttpDelete("document-requirements/{id:guid}")]
    [Authorize(Roles = RoleGroups.Admins)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDocumentRequirement(Guid id)
    {
        await _mediator.Send(
            new Application.Projects.DocumentRequirements.DeleteProjectDocumentRequirementCommand { Id = id });
        return NoContent();
    }

    /// <summary>
    /// Adds a neighbourhood amenity to a project's "Quartier" tab (§7.2).
    /// Re-adding an existing name returns that row rather than duplicating it.
    /// </summary>
    [HttpPost("{projectId:guid}/quartier-amenities")]
    [Authorize(Roles = RoleGroups.Admins)]
    [ProducesResponseType(typeof(QuartierAmenityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddQuartierAmenity(
        Guid projectId,
        [FromBody] Application.Projects.QuartierAmenities.AddQuartierAmenityCommand command)
    {
        command.ProjectId = projectId;
        return Ok(await _mediator.Send(command));
    }

    /// <summary>Removes one amenity chip (§7.2 presentation data).</summary>
    [HttpDelete("quartier-amenities/{id:guid}")]
    [Authorize(Roles = RoleGroups.Admins)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveQuartierAmenity(Guid id)
    {
        await _mediator.Send(new Application.Projects.QuartierAmenities.RemoveQuartierAmenityCommand { Id = id });
        return NoContent();
    }

    [HttpGet("features")]
    [AllowAnonymous] // §6.3 Catalogue public.
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProjectFeatures([FromQuery] GetProjectFeaturesQuery query)
    {
        // A project with no amenities is a normal, valid state — not a missing
        // resource. 404 here made every fetch throw client-side (masked by
        // projectFetchOr404Empty's fallback, but still noisy failed requests
        // in the console on every single project detail page load).
        var features = await _mediator.Send(query);
        return Ok(features ?? new List<ProjectFeatureResponse>());
    }

    /// <summary>
    /// Creates a new quartier (district/area).
    /// </summary>
    /// <param name="command">The command containing quartier details (name, description, images).</param>
    /// <returns>Returns a response containing the newly created quartier ID and a success message.</returns>
    [HttpPost("quartiers")]
    [Authorize(Roles = RoleGroups.Admins)]
    [ProducesResponseType(typeof(CreateQuartierResponse), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateQuartier([FromBody] CreateQuartierCommand command)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var response = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetQuartierById), new { id = response.Id }, response);
    }

    [HttpGet("quartiers")]
    [AllowAnonymous] // §6.3 Catalogue public (quartier = donnée publique §7.2).
    [ProducesResponseType(typeof(PaginatedResponse<QuartierListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetQuartiers([FromQuery] GetQuartiersQuery query)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var response = await _mediator.Send(query);
        return Ok(response);
    }
    /// <summary>
    /// Retrieves a quartier by its unique identifier.
    /// </summary>
    /// <param name="id">The ID of the quartier to retrieve.</param>
    /// <returns>The quartier details if found, otherwise a 404 Not Found.</returns>
    [HttpGet("quartiers/{id}")]
    [AllowAnonymous] // §6.3 Catalogue public.
    [ProducesResponseType(typeof(GetQuartierByIdResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetQuartierById(Guid id)
    {
        var query = new GetQuartierByIdQuery { Id = id };
        var response = await _mediator.Send(query);

        if (response == null)
        {
            return NotFound($"Quartier with ID {id} was not found.");
        }

        return Ok(response);
    }

    /// <summary>
    /// Corrects a quartier (§7.2 referential). Deletion is a separate action,
    /// refused while projects still reference the quartier.
    /// </summary>
    [HttpPut("quartiers/{id:guid}")]
    [Authorize(Roles = RoleGroups.Admins)]
    [ProducesResponseType(typeof(Application.Quartiers.UpdateQuartier.QuartierResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateQuartier(
        Guid id,
        [FromBody] Application.Quartiers.UpdateQuartier.UpdateQuartierCommand command)
    {
        command.Id = id;
        return Ok(await _mediator.Send(command));
    }

    /// <summary>The quartier's features (title, description, optional image), in display order.</summary>
    [HttpGet("quartiers/{quartierId:guid}/features")]
    [AllowAnonymous] // §6.3 Catalogue public, like the quartier itself.
    public async Task<IActionResult> GetQuartierFeatures(Guid quartierId) =>
        Ok(await _mediator.Send(new Application.Quartiers.Features.GetQuartierFeaturesQuery { QuartierId = quartierId }));

    /// <summary>Adds a feature to a quartier.</summary>
    [HttpPost("quartiers/{quartierId:guid}/features")]
    [Authorize(Roles = RoleGroups.Admins)]
    public async Task<IActionResult> CreateQuartierFeature(Guid quartierId, [FromBody] Application.Quartiers.Features.CreateQuartierFeatureCommand command)
    {
        command.QuartierId = quartierId;
        return Ok(await _mediator.Send(command));
    }

    /// <summary>Edits a quartier feature (an empty image removes it).</summary>
    [HttpPut("quartier-features/{id:guid}")]
    [Authorize(Roles = RoleGroups.Admins)]
    public async Task<IActionResult> UpdateQuartierFeature(Guid id, [FromBody] Application.Quartiers.Features.UpdateQuartierFeatureCommand command)
    {
        command.Id = id;
        return Ok(await _mediator.Send(command));
    }

    /// <summary>Removes a quartier feature.</summary>
    [HttpDelete("quartier-features/{id:guid}")]
    [Authorize(Roles = RoleGroups.Admins)]
    public async Task<IActionResult> DeleteQuartierFeature(Guid id)
    {
        await _mediator.Send(new Application.Quartiers.Features.DeleteQuartierFeatureCommand { Id = id });
        return NoContent();
    }

    /// <summary>Deletes an unreferenced quartier (409 while a project is attached to it).</summary>
    [HttpDelete("quartiers/{id:guid}")]
    [Authorize(Roles = RoleGroups.Admins)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteQuartier(Guid id)
    {
        await _mediator.Send(new Application.Quartiers.DeleteQuartier.DeleteQuartierCommand { Id = id });
        return NoContent();
    }

    /// <summary>
    /// Corrects a published site video (§7.2). Creation existed with no way to
    /// fix a mistyped link afterwards.
    /// </summary>
    [HttpPut("videos/{id:guid}")]
    [Authorize(Roles = RoleGroups.Admins)]
    [ProducesResponseType(typeof(Application.EspacesTempsReel.ManageEspaceTempsReel.EspaceTempsReelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEspaceTempsReel(
        Guid id,
        [FromBody] Application.EspacesTempsReel.ManageEspaceTempsReel.UpdateEspaceTempsReelCommand command)
    {
        command.Id = id;
        return Ok(await _mediator.Send(command));
    }

    /// <summary>Removes a site video from the project's public page (§7.2).</summary>
    [HttpDelete("videos/{id:guid}")]
    [Authorize(Roles = RoleGroups.Admins)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEspaceTempsReel(Guid id)
    {
        await _mediator.Send(new Application.EspacesTempsReel.ManageEspaceTempsReel.DeleteEspaceTempsReelCommand { Id = id });
        return NoContent();
    }

    [HttpPost("{projectId}/videos")]
    [Authorize(Roles = RoleGroups.Admins)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateEspaceTempsReel(Guid projectId, [FromBody] CreateEspaceTempsReelCommand command)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Ensure the route ID matches the command's ProjectId
        command.ProjectId = projectId;

        var response = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetEspaceTempsReelById),
                               new { id = response.Id },
                               response);
    }
    /// <summary>
    /// Retrieves a single EspaceTempsReel entry by its unique identifier.
    /// </summary>
    /// <param name="id">The ID of the EspaceTempsReel to retrieve.</param>
    /// <returns>The EspaceTempsReel details if found, otherwise a 404.</returns>
    [HttpGet("videos/{id}")]
    [AllowAnonymous] // §6.3 Catalogue public (lien 3D/vidéo, §8.3 FR-PUB-007).
    public async Task<IActionResult> GetEspaceTempsReelById(Guid id)
    {
        var query = new GetEspaceTempsReelByIdQuery { Id = id };
        var result = await _mediator.Send(query);

        return Ok(result);
    }

    /// <summary>
    /// Retrieves all videos (EspaceTempsReel) for a specific project.
    /// </summary>
    /// <param name="projectId">The ID of the project.</param>
    /// <returns>A list of videos associated with the project.</returns>
    [HttpGet("{projectId}/videos")]
    [AllowAnonymous] // §6.3 Catalogue public.
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVideosByProjectId(Guid projectId)
    {
        var query = new Application.EspacesTempsReel.GetVideosByProjectId.GetVideosByProjectIdQuery { ProjectId = projectId };
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a paginated list of purchases for the specified user along with aggregated totals.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="pageNumber">The page number (default is 1).</param>
    /// <param name="pageSize">The page size (default is 10).</param>
    /// <returns>A paginated response containing purchase details and totals.</returns>
    [HttpGet("user/{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserPurchases(string userId, int pageNumber = 1, int pageSize = 10)
    {
        var query = new GetUserPurchasesQuery
        {
            UserId = userId,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var response = await _mediator.Send(query);
        return Ok(response);
    }

    /// <summary>
    /// Removes a project by its ID.
    /// </summary>
    /// <param name="projectId">The ID of the project to remove.</param>
    /// <returns>The result of the removal operation.</returns>
    [HttpDelete("{projectId}")]
    [Authorize(Roles = RoleGroups.Admins)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveProject(Guid projectId)
    {
        var command = new RemoveProjectCommand { ProjectId = projectId };
        var response = await _mediator.Send(command);
        
        if (!response.Success)
            return BadRequest(response.Message);
            
        return Ok(response);
    }

    /// <summary>
    /// Associates an existing TypeBien to a Project.
    /// </summary>
    [HttpPost("{projectId}/type-biens")]
    [Authorize(Roles = RoleGroups.Admins)]
    public async Task<IActionResult> AssociateTypeBienToProject(Guid projectId, [FromBody] AssociateTypeBienToProjectCommand command)
    {
        command.ProjectId = projectId;
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    /// <summary>
    /// Gets the list of TypeBiens associated with a specific Project.
    /// </summary>
    [HttpGet("{projectId}/type-biens")]
    [AllowAnonymous] // §6.3 Catalogue public.
    public async Task<IActionResult> GetTypeBiensByProject(Guid projectId)
    {
        var query = new GetTypeBiensByProjectQuery { ProjectId = projectId };
        var response = await _mediator.Send(query);
        return Ok(response);
    }

    /// <summary>
    /// Gets all TypeBiens (when no projectId is specified).
    /// </summary>
    [HttpGet("type-biens")]
    [AllowAnonymous] // §6.3 Catalogue public.
    public async Task<IActionResult> GetAllTypeBiens()
    {
        var query = new GetTypeBiensByProjectQuery { ProjectId = null };
        var response = await _mediator.Send(query);
        return Ok(response);
    }
}