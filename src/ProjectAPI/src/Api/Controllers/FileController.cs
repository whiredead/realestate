using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Common.BlobOperations.DeleteFromBlob;
using ProjectAPI.Api.Application.Common.BlobOperations.UpdateBlob;
using ProjectAPI.Api.Application.Common.BlobOperations.UploadToblob;
using ProjectAPI.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace ProjectAPI.Api.Controllers;

/// <summary>
/// Controller for file upload operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize] // §24.2/§34 — documents privés: no anonymous upload/download/delete.
public class FileController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IBlobStorageService _blobStorageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileController"/> class.
    /// </summary>
    /// <param name="mediator">Mediator instance.</param>
    /// <param name="blobStorageService">Blob storage service for file downloads.</param>
    public FileController(IMediator mediator, IBlobStorageService blobStorageService)
    {
        _mediator = mediator;
        _blobStorageService = blobStorageService;
    }

    /// <summary>
    /// Uploads a list of files to blob storage.
    /// </summary>
    /// <param name="command">The command containing the list of files to upload.</param>
    /// <returns>
    /// Returns the list of file links if successful.
    /// Returns a bad request if no files are uploaded or an error occurs.
    /// </returns>
    [HttpPost("upload")]
    // Catalogue media (public images): staff who manage stock only. Any signed-in
    // account — a buyer included — could upload, overwrite or delete them.
    [Authorize(Roles = RoleGroups.AdminsAgents)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadFiles(UploadFilesCommand command)
    {
        if (command.Files == null || command.Files.Count == 0)
        {
            return BadRequest("No files uploaded.");
        }

        var response = await _mediator.Send(command);
        return Ok(response);
    }

    /// <summary>
    /// Deletes a file from blob storage.
    /// </summary>
    /// <param name="fileName">The name of the file to delete.</param>
    /// <returns>
    /// Returns a success message if the file is deleted successfully.
    /// Returns a bad request if the file does not exist or cannot be deleted.
    /// </returns>
    [HttpDelete("delete/{fileName}")]
    // Catalogue media (public images): staff who manage stock only. Any signed-in
    // account — a buyer included — could upload, overwrite or delete them.
    [Authorize(Roles = RoleGroups.AdminsAgents)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteFile(string fileName)
    {
        var command = new DeleteFileCommand { FileName = fileName };
        var response = await _mediator.Send(command);
        return response.IsSuccess ? Ok(response.Message) : BadRequest(response.Message);
    }

    /// <summary>
    /// Updates a file in blob storage.
    /// </summary>
    /// <param name="command">The command containing the name of the file to update and the new file data.</param>
    /// <returns>
    /// Returns the new file link if the update is successful.
    /// Returns a bad request if the file does not exist, cannot be updated, or no file is uploaded.
    /// </returns>
    [HttpPut("update/{fileName}")]
    // Catalogue media (public images): staff who manage stock only. Any signed-in
    // account — a buyer included — could upload, overwrite or delete them.
    [Authorize(Roles = RoleGroups.AdminsAgents)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateFile(UpdateFileCommand command)
    {
        if (command.File == null || command.File.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        var response = await _mediator.Send(command);
        return response.IsSuccess ? Ok(response.FileLink) : BadRequest(response.Message);
    }

    /// <summary>
    /// Downloads a file from blob storage.
    /// </summary>
    /// <param name="fileName">The name of the file to download.</param>
    /// <returns>
    /// Returns the file content if found.
    /// Returns 404 Not Found if the file does not exist.
    /// </returns>
    [HttpGet("download/{fileName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadFile(string fileName)
    {
        var (content, contentType, exists) = await _blobStorageService.ForContainer().DownloadAsync(fileName, HttpContext.RequestAborted);

        if (!exists || content == null)
        {
            return NotFound($"File '{fileName}' not found.");
        }

        return File(content, contentType ?? "application/octet-stream", fileName);
    }
}
