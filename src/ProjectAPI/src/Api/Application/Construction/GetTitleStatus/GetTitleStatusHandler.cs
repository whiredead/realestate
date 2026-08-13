using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Construction.GetTitleStatus;

/// <summary>
/// §16 — reachable both from an internal unit page and a buyer's own
/// property file (see FRONTEND_BACKEND_INTEGRATION.md), so both perimeters
/// apply: staff are scoped to their assigned project, buyers to a unit they
/// actually hold a reservation on. Without either check this leaked any
/// unit's title history — including internal Reason/DocumentUrl fields — to
/// any authenticated caller regardless of role or project.
/// </summary>
public class GetTitleStatusHandler : IRequestHandler<GetTitleStatusQuery, GetTitleStatusResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public GetTitleStatusHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<GetTitleStatusResponse> Handle(GetTitleStatusQuery request, CancellationToken ct)
    {
        await _projectScope.EnsureUnitProjectAccessAsync(request.UnitId, ct);
        await _projectScope.EnsureBuyerOwnsUnitAsync(request.UnitId, ct);

        var state = await _db.Set<UnitTitleState>()
            .FirstOrDefaultAsync(t => t.UnitId == request.UnitId, ct);

        var history = await _db.Set<UnitTitleHistory>()
            .Where(h => h.UnitId == request.UnitId)
            .OrderByDescending(h => h.OccurredAt)
            .Select(h => new TitleHistoryEntryDto
            {
                Id = h.Id,
                FromStatus = h.FromStatus != null ? h.FromStatus.ToString() : null,
                ToStatus = h.ToStatus.ToString(),
                OccurredAt = h.OccurredAt,
                Reason = h.Reason,
                DocumentUrl = h.DocumentUrl
            })
            .ToListAsync(ct);

        var status = state?.Status ?? TitleStatus.NotAvailable;

        return new GetTitleStatusResponse
        {
            UnitId = request.UnitId,
            Status = status.ToString(),
            StatusAt = state?.StatusAt,
            DocumentUrl = state?.DocumentUrl,
            AllowsNotaryAppointment = TitleStateMachine.AllowsNotaryAppointment(status),
            History = history
        };
    }
}
