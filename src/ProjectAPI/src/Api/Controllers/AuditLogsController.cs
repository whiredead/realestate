using Microsoft.AspNetCore.Authorization;
using ProjectAPI.Api.Application.AuditLogs;
using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.Common.Security;

namespace ProjectAPI.Api.Controllers;

/// <summary>
/// Audit journal (§5.3). Read-only by design: rows are written by the audit
/// pipeline in the transaction of each change, and no endpoint edits or deletes them.
/// </summary>
[ApiController, Route("api/audit-logs"), Authorize(Roles = RoleGroups.Admins)]
public sealed class AuditLogsController : ControllerBase
{
    private readonly IMediator _mediator;
    public AuditLogsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<AuditLogDto>>> List([FromQuery] GetAuditLogsQuery query, CancellationToken ct) =>
        Ok(await _mediator.Send(query, ct));

    [HttpGet("actions")]
    public async Task<ActionResult<List<string>>> Actions(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetAuditActionsQuery(), ct));
}
