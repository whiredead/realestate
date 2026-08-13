using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Infrastructure.Context;
using UnitEntity = ProjectAPI.Domain.Immeubles.Entities.Unit;

namespace ProjectAPI.Api.Application.Immeubles.GetImmeubleFloorStats;

/// <summary>
/// [AllowAnonymous]-eligible like GetFloorsByImmeubleHandler (§6.3 catalogue
/// public: "structure d'un immeuble") — floor-level stock counts are the
/// same class of public catalogue data as the building-level ones already
/// exposed unauthenticated via GetImmeubleByIdHandler.
/// </summary>
public class GetImmeubleFloorStatsHandler : IRequestHandler<GetImmeubleFloorStatsQuery, List<FloorStatsDto>>
{
    private readonly ApplicationDbContext _db;

    public GetImmeubleFloorStatsHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<FloorStatsDto>> Handle(GetImmeubleFloorStatsQuery request, CancellationToken ct)
    {
        var floors = await _db.Set<Floor>()
            .Where(f => f.ImmeubleId == request.ImmeubleId)
            .OrderBy(f => f.SequenceNo)
            .Select(f => new { f.Id, f.Name, f.SequenceNo })
            .ToListAsync(ct);

        var units = await _db.Set<UnitEntity>()
            .Where(u => u.Floor.ImmeubleId == request.ImmeubleId)
            .Select(u => new { u.FloorId, u.Status })
            .ToListAsync(ct);

        return floors.Select(f =>
        {
            var floorUnits = units.Where(u => u.FloorId == f.Id).ToList();
            return new FloorStatsDto
            {
                Id = f.Id,
                Name = f.Name,
                SequenceNo = f.SequenceNo,
                TotalUnits = floorUnits.Count,
                AvailableUnits = floorUnits.Count(u => u.Status == UnitCommercialStatus.Available),
                ReservedUnits = floorUnits.Count(u => u.Status is UnitCommercialStatus.HoldPendingApproval or UnitCommercialStatus.Reserved or UnitCommercialStatus.Contracted),
                SoldUnits = floorUnits.Count(u => u.Status is UnitCommercialStatus.Sold or UnitCommercialStatus.Delivered),
            };
        }).ToList();
    }
}
