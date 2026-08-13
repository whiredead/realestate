using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Floors.GetFloorsByImmeuble;

public class GetFloorsByImmeubleHandler : IRequestHandler<GetFloorsByImmeubleQuery, List<FloorSummary>>
{
    private readonly ApplicationDbContext _db;

    public GetFloorsByImmeubleHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<FloorSummary>> Handle(GetFloorsByImmeubleQuery request, CancellationToken ct)
    {
        return await _db.Set<Floor>()
            .Where(f => f.ImmeubleId == request.ImmeubleId)
            .OrderBy(f => f.SequenceNo)
            .Select(f => new FloorSummary { Id = f.Id, Name = f.Name, SequenceNo = f.SequenceNo })
            .ToListAsync(ct);
    }
}
