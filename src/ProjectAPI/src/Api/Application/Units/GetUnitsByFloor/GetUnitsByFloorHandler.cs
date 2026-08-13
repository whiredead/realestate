using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;
using UnitEntity = ProjectAPI.Domain.Immeubles.Entities.Unit;

namespace ProjectAPI.Api.Application.Units.GetUnitsByFloor;

/// <summary>
/// [AllowAnonymous]-eligible for the base unit fields, same as
/// GetUnitsByProjectIdHandler — but buyer/agent enrichment is only ever
/// populated for units already in a non-selectable state (RESERVED and
/// beyond), which is public catalogue information already ("this unit isn't
/// available") rather than a new PII exposure; agent/buyer names are the
/// same fields other admin screens already surface for a converted sale.
/// </summary>
public class GetUnitsByFloorHandler : IRequestHandler<GetUnitsByFloorQuery, List<UnitDrillDownDto>>
{
    private readonly ApplicationDbContext _db;

    public GetUnitsByFloorHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<UnitDrillDownDto>> Handle(GetUnitsByFloorQuery request, CancellationToken ct)
    {
        var units = await _db.Set<UnitEntity>()
            .Where(u => u.FloorId == request.FloorId)
            .Select(u => new
            {
                u.Id,
                u.FloorId,
                u.UnitNumber,
                u.NumberOfBedrooms,
                u.NumberOfBathrooms,
                u.ApartmentSurface,
                u.TotalSurface,
                u.View,
                u.Orientation,
                u.LatestPrice,
                u.Status,
                u.Images,
            })
            .ToListAsync(ct);

        var unitIds = units.Select(u => u.Id).ToHashSet();

        // The active reservation per unit — not Rejected/Cancelled — is the
        // one that explains why a non-AVAILABLE unit is in that state. A
        // unit can have several historical reservations (e.g. one rejected,
        // then a later one approved — the exact A102 shape this session's
        // own fixture work produced); only the live one is shown here.
        var activeReservations = await _db.Set<Reservation>()
            .Where(r => unitIds.Contains(r.UnitId) && r.Status != ReservationStatus.Rejected && r.Status != ReservationStatus.Cancelled)
            .Select(r => new { r.UnitId, r.Name, r.LastName, r.AgentId, r.CreatedAt })
            .ToListAsync(ct);

        var reservationByUnit = activeReservations
            .GroupBy(r => r.UnitId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.CreatedAt).First());

        var agentIds = reservationByUnit.Values
            .Where(r => !string.IsNullOrEmpty(r.AgentId))
            .Select(r => r.AgentId!)
            .Distinct()
            .ToList();
        var agents = await _db.Users
            .Where(u => agentIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToListAsync(ct);
        var agentNameById = agents.ToDictionary(a => a.Id, a => $"{a.FirstName} {a.LastName}".Trim());

        return units.Select(u =>
        {
            reservationByUnit.TryGetValue(u.Id, out var reservation);
            var buyerName = reservation is null
                ? null
                : string.Join(" ", new[] { reservation.Name, reservation.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));

            return new UnitDrillDownDto
            {
                Id = u.Id,
                FloorId = u.FloorId,
                UnitNumber = u.UnitNumber,
                NumberOfBedrooms = u.NumberOfBedrooms,
                NumberOfBathrooms = u.NumberOfBathrooms,
                ApartmentSurface = u.ApartmentSurface,
                TotalSurface = u.TotalSurface,
                View = u.View,
                Orientation = u.Orientation,
                LatestPrice = u.LatestPrice,
                Status = u.Status.ToCode(),
                Images = u.Images,
                BuyerName = string.IsNullOrWhiteSpace(buyerName) ? null : buyerName,
                AgentId = reservation?.AgentId,
                AgentName = reservation?.AgentId is not null ? agentNameById.GetValueOrDefault(reservation.AgentId) : null,
            };
        }).OrderBy(u => u.UnitNumber).ToList();
    }
}
