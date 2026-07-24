using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Construction.AddMilestone;
using ProjectAPI.Api.Application.Construction.CompleteProject;
using ProjectAPI.Api.Application.Construction.PublishConstructionUpdate;
using ProjectAPI.Api.Application.Construction.UpdateMilestoneStatus;
using ProjectAPI.Api.Application.Construction.UpdateTitleStatus;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Controllers;

/// <summary>
/// Construction tracking (spec §15) and land-title status (§16).
/// </summary>
[ApiController]
[Route("api/construction")]
public class ConstructionController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ApplicationDbContext _db;

    public ConstructionController(IMediator mediator, ApplicationDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    /// <summary>Milestones and published updates for a project (§15).</summary>
    [HttpGet("projects/{projectId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProjectConstruction(Guid projectId, CancellationToken ct)
    {
        var milestones = await _db.Set<ConstructionMilestone>()
            .Where(m => m.ProjectId == projectId)
            .OrderBy(m => m.SequenceNo)
            .Select(m => new
            {
                m.Id,
                m.Code,
                m.NameFr,
                m.NameEn,
                m.SequenceNo,
                m.WeightPercent,
                m.PlannedDate,
                m.ActualDate,
                Status = m.Status.ToString(),
                m.VisibleToBuyer,
                m.VisibleToPublic
            })
            .ToListAsync(ct);

        // Superseded versions are excluded: a correction creates a new version
        // rather than editing the published one (§15.2 FR-CON-004).
        var updates = await _db.Set<ConstructionUpdate>()
            .Where(u => u.ProjectId == projectId)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new
            {
                u.Id,
                u.VersionNo,
                u.ProgressPercent,
                u.TitleFr,
                u.DescriptionFr,
                u.MediaUrls,
                Visibility = u.Visibility.ToString(),
                u.PublishedAt,
                u.CreatedAt
            })
            .ToListAsync(ct);

        // Overall progress is DERIVED from completed milestone weights (§5.8),
        // not read from a stored field that could drift.
        var totalWeight = milestones.Sum(m => m.WeightPercent);
        var doneWeight = milestones
            .Where(m => m.Status == MilestoneStatus.Completed.ToString())
            .Sum(m => m.WeightPercent);

        var progress = totalWeight > 0 ? Math.Round(doneWeight / totalWeight * 100, 2) : 0;

        return Ok(new
        {
            projectId,
            milestones,
            updates,
            computedProgressPercent = progress,
            calculatedAt = DateTime.UtcNow
        });
    }

    /// <summary>Land-title status and history for a unit (§16).</summary>
    [HttpGet("units/{unitId:guid}/title")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTitle(Guid unitId, CancellationToken ct)
    {
        var state = await _db.Set<UnitTitleState>()
            .FirstOrDefaultAsync(t => t.UnitId == unitId, ct);

        var history = await _db.Set<UnitTitleHistory>()
            .Where(h => h.UnitId == unitId)
            .OrderByDescending(h => h.OccurredAt)
            .Select(h => new
            {
                h.Id,
                FromStatus = h.FromStatus != null ? h.FromStatus.ToString() : null,
                ToStatus = h.ToStatus.ToString(),
                h.OccurredAt,
                h.Reason,
                h.DocumentUrl
            })
            .ToListAsync(ct);

        var status = state?.Status ?? TitleStatus.NotAvailable;

        return Ok(new
        {
            unitId,
            status = status.ToString(),
            statusAt = state?.StatusAt,
            documentUrl = state?.DocumentUrl,
            allowsNotaryAppointment = TitleStateMachine.AllowsNotaryAppointment(status),
            history
        });
    }

    /// <summary>Moves the title status, tracing the change (§16).</summary>
    [HttpPatch("units/{unitId:guid}/title")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateTitle(Guid unitId, [FromBody] UpdateTitleStatusCommand body)
    {
        body.UnitId = unitId;
        return Ok(await _mediator.Send(body));
    }

    /// <summary>Creates a weighted milestone (§15.1).</summary>
    [HttpPost("projects/{projectId:guid}/milestones")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddMilestone(Guid projectId, [FromBody] AddMilestoneCommand body)
    {
        body.ProjectId = projectId;
        return Ok(await _mediator.Send(body));
    }

    /// <summary>Updates a milestone's status (§15.1 FR-CON-002).</summary>
    [HttpPatch("milestones/{milestoneId:guid}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMilestoneStatus(
        Guid milestoneId,
        [FromBody] UpdateMilestoneStatusCommand body)
    {
        body.MilestoneId = milestoneId;
        return Ok(await _mediator.Send(body));
    }

    /// <summary>Publishes a progress update (§15.2).</summary>
    [HttpPost("projects/{projectId:guid}/updates")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PublishUpdate(Guid projectId, [FromBody] PublishConstructionUpdateCommand body)
    {
        body.ProjectId = projectId;
        return Ok(await _mediator.Send(body));
    }

    /// <summary>Marks the project COMPLETED, opening final visits (§15.3, §17.1).</summary>
    [HttpPost("projects/{projectId:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CompleteProject(Guid projectId, [FromBody] CompleteProjectCommand body)
    {
        body.ProjectId = projectId;
        return Ok(await _mediator.Send(body));
    }
}
