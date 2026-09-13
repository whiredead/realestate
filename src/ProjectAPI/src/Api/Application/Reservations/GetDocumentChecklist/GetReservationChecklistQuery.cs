using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Reservations;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Reservations.GetDocumentChecklist;

/// <summary>
/// §12.1 — what this reservation still needs before it can be submitted.
///
/// The console asks for this rather than deriving it: the requirement rows are
/// per project and the matching is case-insensitive on DocumentType, and a
/// client-side copy of that rule would drift from the one the submit endpoint
/// actually enforces — showing a green checklist next to a 422.
/// </summary>
public class GetReservationChecklistQuery : IRequest<ReservationChecklistResponse>
{
    public Guid ReservationId { get; set; }
}

public class ReservationChecklistResponse
{
    public Guid ReservationId { get; set; }

    /// <summary>True when no required document is missing — i.e. submit will not be refused on documents.</summary>
    public bool CanSubmit { get; set; }

    public List<ReservationDocumentChecklistItem> Items { get; set; } = new();
}

public class GetReservationChecklistHandler
    : IRequestHandler<GetReservationChecklistQuery, ReservationChecklistResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;
    private readonly ReservationDocumentChecklist _checklist;

    public GetReservationChecklistHandler(
        ApplicationDbContext db,
        ProjectScopeService projectScope,
        ReservationDocumentChecklist checklist)
    {
        _db = db;
        _projectScope = projectScope;
        _checklist = checklist;
    }

    public async Task<ReservationChecklistResponse> Handle(GetReservationChecklistQuery request, CancellationToken ct)
    {
        await _projectScope.EnsureReservationAccessAsync(request.ReservationId, ct);

        var unitId = await _db.Set<Reservation>()
            .Where(r => r.Id == request.ReservationId)
            .Select(r => (Guid?)r.UnitId)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Reservation {request.ReservationId} not found.");

        var items = await _checklist.BuildAsync(request.ReservationId, unitId, ct);

        return new ReservationChecklistResponse
        {
            ReservationId = request.ReservationId,
            CanSubmit = items.TrueForAll(i => !i.IsRequired || i.IsProvided),
            Items = items
        };
    }
}
