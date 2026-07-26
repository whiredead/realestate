using ProjectAPI.Api.Application.TypeBiens.CreateTypeBien;
using ProjectAPI.Api.Application.TypeBiens.DeleteTypeBien;
using ProjectAPI.Api.Application.TypeBiens.UpdateTypeBien;
using ProjectAPI.Api.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;

namespace ProjectAPI.Api.Controllers;

/// <summary>
/// Controller for managing TypeBiens (property types — reference data).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = RoleGroups.Admins)] // Reference data managed by admins (§6.3 stock/paramétrage).
public class TypeBiensController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeBiensController"/> class.
    /// </summary>
    /// <param name="mediator">The mediator to handle commands and queries.</param>
    public TypeBiensController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Creates a new TypeBien.
    /// </summary>
    /// <param name="command">The command containing the details for the new TypeBien.</param>
    /// <returns>The response containing the unique identifier of the created TypeBien.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTypeBien([FromBody] CreateTypeBienCommand command)
    {
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    /// <summary>
    /// Updates a TypeBien by its ID.
    /// </summary>
    /// <param name="id">The ID of the TypeBien to update.</param>
    /// <param name="command">The TypeBien update details.</param>
    /// <returns>The updated TypeBien details.</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTypeBien(int id, [FromBody] UpdateTypeBienCommand command)
    {
        if (id != command.Id)
        {
            return BadRequest("TypeBien ID in the URL does not match the command ID.");
        }

        var response = await _mediator.Send(command);
        return response.IsSuccess ? Ok(response) : BadRequest(response);
    }

    /// <summary>
    /// Deletes a TypeBien by its ID.
    /// </summary>
    /// <param name="id">The ID of the TypeBien to delete.</param>
    /// <returns>The result of the delete operation.</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTypeBien(int id)
    {
        var command = new DeleteTypeBienCommand { Id = id };
        var response = await _mediator.Send(command);
        return response.IsSuccess ? Ok(response) : BadRequest(response);
    }
}