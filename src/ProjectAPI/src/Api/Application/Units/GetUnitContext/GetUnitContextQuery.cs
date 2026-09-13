using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Common.Units;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Units.GetUnitContext;

/// <summary>
/// One unit with its floor, building and project. The console used to locate a
/// unit by listing every building and then every building's units (one request
/// per building), which grew with the portfolio until the unit page never loaded.
/// </summary>
public class GetUnitContextQuery : IRequest<UnitContextDto>
{
    public Guid UnitId { get; set; }
}

public class GetUnitContextHandler : IRequestHandler<GetUnitContextQuery, UnitContextDto>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public GetUnitContextHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<UnitContextDto> Handle(GetUnitContextQuery request, CancellationToken ct)
    {
        var context = (await UnitLocations.ForUnitsAsync(_db, new[] { request.UnitId }, ct)).GetValueOrDefault(request.UnitId)
            ?? throw new NotFoundException($"Unit {request.UnitId} not found.");

        await _projectScope.EnsureUnitProjectAccessAsync(request.UnitId, ct);
        return context;
    }
}
