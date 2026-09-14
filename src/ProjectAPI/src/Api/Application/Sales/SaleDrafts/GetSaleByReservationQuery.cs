using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Sales.SaleDrafts;

/// <summary>
/// The sale attached to a reservation, or <c>null</c> when there is none.
///
/// Returns 200 with a null body rather than 404: "this dossier has no sale yet"
/// is the normal state of every approved reservation, and the reservation page
/// asks this question on every load to decide whether to offer "Créer une
/// vente". A 404 there would be indistinguishable from a broken route.
///
/// A cancelled sale is deliberately still returned when it is the only one —
/// the page must be able to say a sale was abandoned, not silently look as if
/// none ever existed.
/// </summary>
public class GetSaleByReservationQuery : IRequest<SaleResponse?>
{
    public Guid ReservationId { get; set; }
}

public class GetSaleByReservationHandler : IRequestHandler<GetSaleByReservationQuery, SaleResponse?>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public GetSaleByReservationHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<SaleResponse?> Handle(GetSaleByReservationQuery request, CancellationToken ct)
    {
        // Both checks: internal callers by project perimeter, a buyer by
        // ownership of the file. Either one alone would admit the other shape.
        await _projectScope.EnsureReservationAccessAsync(request.ReservationId, ct);
        await _projectScope.EnsureBuyerOwnsReservationAsync(request.ReservationId, ct);

        var activeStatuses = SaleStateMachine.ActiveStatuses;

        // At most one ACTIVE sale can exist (IX_Sales_ActivePerReservation), but
        // any number of cancelled ones may: order so the active one always wins,
        // then the most recent cancellation.
        var sale = await _db.Set<Sale>()
            .Where(s => s.ReservationId == request.ReservationId)
            .OrderByDescending(s => activeStatuses.Contains(s.Status))
            .ThenByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return sale is null ? null : await SaleResponse.FromAsync(_db, sale, ct);
    }
}
