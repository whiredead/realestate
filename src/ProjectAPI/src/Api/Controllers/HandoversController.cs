using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Handovers;

namespace ProjectAPI.Api.Controllers;

/// <summary>
/// Remise des clés — spec §5.8 / §19. The final step of the sale: it is what
/// moves a unit to DELIVERED and starts the warranty that SAV depends on.
/// </summary>
[ApiController]
[Route("api/Handovers")]
[Authorize]
public class HandoversController : ControllerBase
{
    private readonly IMediator _mediator;

    public HandoversController(IMediator mediator) => _mediator = mediator;

    /// <summary>Plans the handover. Requires a purchase finalised at the notary.</summary>
    [HttpPost]
    [Authorize(Roles = RoleGroups.AdminsAgents)] // §6.3 Livraison "C/M affecté".
    public async Task<IActionResult> Schedule([FromBody] ScheduleHandoverCommand command)
        => Ok(new { id = await _mediator.Send(command) });

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Roles = RoleGroups.AdminsAgents)]
    public async Task<IActionResult> Confirm(Guid id)
        => Ok(await _mediator.Send(new ConfirmHandoverCommand { AppointmentId = id }));

    /// <summary>
    /// Records the procès-verbal. Does NOT deliver the property — the buyer must
    /// acknowledge it first (§19.3).
    /// </summary>
    [HttpPost("{id:guid}/report")]
    [Authorize(Roles = RoleGroups.AdminsAgents)]
    public async Task<IActionResult> SubmitReport(Guid id, [FromBody] SubmitHandoverReportCommand command)
    {
        command.AppointmentId = id;
        return Ok(new { id = await _mediator.Send(command) });
    }

    /// <summary>
    /// The buyer confirms receipt — the step that actually delivers the property
    /// (§5.8): unit → DELIVERED, warranty starts, SAV opens. Idempotent.
    ///
    /// Open to the buyer as well as staff, because it is the buyer's own act. The
    /// agent may record it on their behalf for a buyer with no account (§1.1).
    /// </summary>
    [HttpPost("reports/{reportId:guid}/acknowledge")]
    [Authorize]
    public async Task<IActionResult> Acknowledge(Guid reportId, [FromBody] AcknowledgeHandoverCommand? command)
        => Ok(await _mediator.Send(new AcknowledgeHandoverCommand
        {
            ReportId = reportId,
            WarrantyMonths = command?.WarrantyMonths ?? 12,
        }));
}
