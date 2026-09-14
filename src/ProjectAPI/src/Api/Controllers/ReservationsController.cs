using ProjectAPI.Api.Application.Reservations.ApproveReservation;
using ProjectAPI.Api.Application.Reservations.AssignNotaireToReservation;
using ProjectAPI.Api.Application.Reservations.CancelReservation;
using ProjectAPI.Api.Application.Reservations.CreateReservation;
using ProjectAPI.Api.Application.Reservations.DeleteReservationDocument;
using ProjectAPI.Api.Application.Reservations.GetMyReservations;
using ProjectAPI.Api.Application.Reservations.GetReservationById;
using ProjectAPI.Api.Application.Reservations.GetReservations;
using ProjectAPI.Api.Application.Reservations.RejectReservation;
using ProjectAPI.Api.Application.Reservations.RequestChanges;
using ProjectAPI.Api.Application.Reservations.ResubmitReservation;
using ProjectAPI.Api.Application.Reservations.UploadReservationDocument;
using ProjectAPI.Api.Application.Common.Idempotency;
using ProjectAPI.Api.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;

namespace ProjectAPI.Api.Controllers;
[ApiController]
[Route("api/[controller]")]
[Authorize] // §6.3 Réservation — no anonymous access. Per-action roles below.
public class ReservationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReservationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("create")]
    [Authorize(Roles = RoleGroups.AdminsAgents)] // §6.3 "C/M soumission" — agent (ou admin).
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateReservation([FromBody] CreateReservationCommand command)
    {
        command.IdempotencyKey ??= Request.GetIdempotencyKey();
        var response = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetReservationById), new { id = response.ReservationId }, response);
    }

    /// <summary>
    /// Corrects a DRAFT or CHANGES_REQUESTED reservation (§12.2). Refused (409)
    /// once submitted or decided — see the command for why.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleGroups.AdminsAgents)] // §6.3 "C/M soumission" — the agent who owns the file corrects it.
    [ProducesResponseType(typeof(Application.Reservations.UpdateReservation.UpdateReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateReservation(
        Guid id,
        [FromBody] Application.Reservations.UpdateReservation.UpdateReservationCommand command)
    {
        command.ReservationId = id;
        return Ok(await _mediator.Send(command));
    }

    /// <summary>
    /// §12.1 — the document checklist for this reservation: what the project
    /// requires, and what has already been attached. Empty when the project
    /// declares no requirements.
    /// </summary>
    [HttpGet("{id:guid}/document-checklist")]
    [Authorize(Roles = RoleGroups.AdminsAgents)]
    [ProducesResponseType(typeof(Application.Reservations.GetDocumentChecklist.ReservationChecklistResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocumentChecklist(Guid id)
    {
        return Ok(await _mediator.Send(
            new Application.Reservations.GetDocumentChecklist.GetReservationChecklistQuery { ReservationId = id }));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = RoleGroups.AdminsAgents)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReservationById(Guid id)
    {
        var response = await _mediator.Send(new GetReservationByIdQuery { ReservationId = id });
        return Ok(response);
    }

    /// <summary>§8 "My properties" — the buyer's own reservations, never anyone else's.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyReservations()
    {
        return Ok(await _mediator.Send(new GetMyReservationsQuery()));
    }

    [HttpGet("list")]
    [Authorize(Roles = RoleGroups.AdminsAgents)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetReservations([FromQuery] GetReservationsQuery query)
    {
        var response = await _mediator.Send(query);
        return Ok(response);
    }

    /// <summary>
    /// Uploads a document (contract, CIN, blueprint…) for this reservation to
    /// blob storage and records it — ReservationDocument existed and was read
    /// (GetReservationById already returns reservation.Documents) but had no
    /// write path anywhere until now.
    /// </summary>
    [HttpPost("{id:guid}/documents")]
    [Authorize(Roles = RoleGroups.AdminsAgents)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadDocument(Guid id, [FromForm] UploadReservationDocumentCommand command)
    {
        if (command.File == null || command.File.Length == 0)
        {
            // A typed 422 with a field error, like every other validation failure,
            // instead of a bare text/plain 400 the frontend cannot parse.
            throw new Application.Common.Exceptions.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure("File", "Aucun fichier n'a été envoyé.")
            });
        }

        command.ReservationId = id;
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    /// <summary>
    /// §24.2/§34 — streams a reservation document to a caller entitled to see
    /// it. The console links here rather than to the raw blob URL: the
    /// container is private, and a document of this kind must not be readable
    /// by anyone who merely holds its address.
    ///
    /// A buyer may fetch their own file's documents, so this is NOT restricted
    /// to AdminsAgents — the handler checks perimeter and ownership instead.
    /// </summary>
    [HttpGet("documents/{documentId:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadDocument(Guid documentId)
    {
        var file = await _mediator.Send(
            new Application.Reservations.DownloadReservationDocument.DownloadReservationDocumentQuery
            {
                DocumentId = documentId
            });

        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpDelete("documents/{documentId:guid}")]
    [Authorize(Roles = RoleGroups.AdminsAgents)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        var response = await _mediator.Send(new DeleteReservationDocumentCommand { DocumentId = documentId });
        return response.IsSuccess ? Ok(response.Message) : BadRequest(response.Message);
    }
    // §6.3 "V périmètre" — validation by the project administrator. §6.4 also
    // forbids the owning agent from approving their own reservation; that check
    // is enforced in ApproveReservationHandler (the caller id isn't visible here).
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = RoleGroups.Admins)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveReservationCommand body)
    {
        body.ReservationId = id;
        body.IdempotencyKey ??= Request.GetIdempotencyKey();
        return Ok(await _mediator.Send(body));
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = RoleGroups.Admins)] // §6.3 "V périmètre".
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectReservationCommand body)
    {
        body.ReservationId = id;
        return Ok(await _mediator.Send(body));
    }

    /// <summary>
    /// Sends a submitted reservation back to the agent for correction (§12.2).
    /// The unit stays blocked.
    /// </summary>
    [HttpPost("{id:guid}/request-changes")]
    [Authorize(Roles = RoleGroups.Admins)] // §12.2 — decision by the project admin.
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestChanges(Guid id, [FromBody] RequestChangesCommand body)
    {
        body.ReservationId = id;
        return Ok(await _mediator.Send(body));
    }

    /// <summary>
    /// Submits a draft, or returns a corrected reservation for a new decision (§12.4).
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = RoleGroups.AdminsAgents)] // §6.3 "C/M soumission" — agent submits/resubmits.
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(Guid id, [FromBody] ResubmitReservationCommand body)
    {
        body.ReservationId = id;
        return Ok(await _mediator.Send(body));
    }

    [HttpPut("{id:guid}/assign-notaire")]
    [Authorize(Roles = RoleGroups.AdminsAgents)] // §6.3 RDV notaire "C/M affecté" — agent/admin.
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignNotaire(Guid id, [FromBody] AssignNotaireToReservationCommand body)
    {
        body.ReservationId = id;
        var response = await _mediator.Send(body);
        return Ok(response);
    }

    [HttpPut("{id:guid}/cancel")]
    [Authorize(Roles = RoleGroups.Admins)] // §12.3 FR-RES-008 — cancellation of an approved reservation is admin-only.
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelReservation(Guid id, [FromBody] CancelReservationCommand body)
    {
        body.ReservationId = id;
        var response = await _mediator.Send(body);
        return Ok(response);
    }

    // PUT {id}/sold intentionally removed (§5.3, §5.7).
    //
    // It converted a reservation to CONVERTED on request, with no notarial act
    // behind it, no outcome recorded and no unit transition — a second door to
    // the single most consequential state change in the workflow. The spec is
    // explicit: "CONVERTED only happens when the notary records
    // PURCHASE_COMPLETED (same transaction sets unit SOLD)".
    //
    // Conversion now happens exactly once, inside
    // PUT /api/NotaryAppointments/{id} when Outcome = PURCHASE_COMPLETED.
}
