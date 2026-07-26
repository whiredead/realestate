using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Projects.GetQuartierAmenities;

public class GetQuartierAmenitiesHandler : IRequestHandler<GetQuartierAmenitiesQuery, List<QuartierAmenityResponse>>
{
    private readonly ApplicationDbContext _db;

    public GetQuartierAmenitiesHandler(ApplicationDbContext db) => _db = db;

    public async Task<List<QuartierAmenityResponse>> Handle(GetQuartierAmenitiesQuery request, CancellationToken cancellationToken)
    {
        return await _db.Set<QuartierAmenity>()
            .Where(a => a.ProjectId == request.ProjectId)
            .Select(a => new QuartierAmenityResponse { Id = a.Id, Name = a.Name, Icon = a.Icon })
            .ToListAsync(cancellationToken);
    }
}
