using ProjectAPI.Api.Application.Common.Units;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Reservations.GetMyReservations;

public class GetMyReservationsHandler : IRequestHandler<GetMyReservationsQuery, List<MyReservationSummary>>
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public GetMyReservationsHandler(ApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<MyReservationSummary>> Handle(GetMyReservationsQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId)) return new List<MyReservationSummary>();

        var rows = await _db.Set<Reservation>()
            .Where(r => r.BuyerId == userId)
            .OrderByDescending(r => r.ReservationDate)
            .Select(r => new MyReservationSummary
            {
                Id = r.Id,
                UnitId = r.UnitId,
                UnitDetails = r.UnitDetails,
                Status = (int)r.Status,
                TotalPropertyPrice = r.TotalPropertyPrice,
                FinalPrice = r.FinalPrice,
                ReservationDate = r.ReservationDate,
                ValidatedAt = r.ValidatedAt
            })
            .ToListAsync(ct);

        var contexts = await UnitLocations.ForUnitsAsync(_db, rows.Select(r => r.UnitId), ct);
        foreach (var row in rows) row.WithLocation(contexts.GetValueOrDefault(row.UnitId));
        return rows;
    }
}
