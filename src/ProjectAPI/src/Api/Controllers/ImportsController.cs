using Microsoft.AspNetCore.Authorization;
using ProjectAPI.Api.Application.Common.Idempotency;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Imports.CommitImportBatch;
using ProjectAPI.Api.Application.Imports.GenerateImportTemplate;
using ProjectAPI.Api.Application.Imports.ValidateImportBatch;

namespace ProjectAPI.Api.Controllers;

/// <summary>
/// Excel stock import (spec §5.11, §23). Admin-only: uploading stock is
/// content/administration work, not something an agent does (§8 project-admin
/// nav lists "Import Excel"; agents get read-only stock consultation).
/// </summary>
[ApiController]
[Route("api/imports")]
[Authorize(Roles = RoleGroups.Admins)]
public class ImportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ImportsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Downloads the template for this project (§5.11) — its TypeBien
    /// dropdown is scoped to the project's own linked types. With
    /// <paramref name="buildingNames"/> (repeat the query param, e.g.
    /// <c>?buildingNames=Bâtiment%20A&amp;buildingNames=Bâtiment%20B</c>), the
    /// workbook ships with one already-named sheet per name; omitted, it
    /// ships a single generic "Exemple" sheet to duplicate by hand.
    /// </summary>
    [HttpGet("projects/{projectId:guid}/template")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTemplate(Guid projectId, [FromQuery] List<string>? buildingNames, CancellationToken ct)
    {
        var bytes = await _mediator.Send(
            new GenerateImportTemplateQuery { ProjectId = projectId, BuildingNames = buildingNames ?? new List<string>() },
            ct);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "gpia-stock-import-template.xlsx");
    }

    /// <summary>Dry-run validation: parses and validates the file without writing anything (§5.11).</summary>
    [HttpPost("projects/{projectId:guid}/validate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Validate(Guid projectId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("Un fichier est requis.");
        }
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Le fichier doit être un classeur Excel (.xlsx).");
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);

        var command = new ValidateImportBatchCommand
        {
            ProjectId = projectId,
            FileName = file.FileName,
            FileContent = stream.ToArray(),
            CreatedBy = User.FindFirst("UserId")?.Value
        };

        return Ok(await _mediator.Send(command, ct));
    }

    /// <summary>Commits a validated batch: the only endpoint that writes to the stock tables (§5.11).</summary>
    [HttpPost("{batchId:guid}/commit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Commit(Guid batchId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("Le fichier validé doit être renvoyé pour confirmer l'import.");
        }
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Le fichier doit être un classeur Excel (.xlsx).");
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);

        var command = new CommitImportBatchCommand
        {
            BatchId = batchId,
            FileContent = stream.ToArray(),
            CommittedBy = User.FindFirst("UserId")?.Value,
            IdempotencyKey = Request.GetIdempotencyKey()
        };

        return Ok(await _mediator.Send(command, ct));
    }
}
