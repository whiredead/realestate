using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Floors.CreateFloor;

public class CreateFloorHandler : IRequestHandler<CreateFloorCommand, CreateFloorResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public CreateFloorHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<CreateFloorResponse> Handle(CreateFloorCommand request, CancellationToken ct)
    {
        var buildingProjectId = await _db.Set<Immeuble>()
            .Where(i => i.Id == request.ImmeubleId)
            .Select(i => (Guid?)i.ProjectId)
            .FirstOrDefaultAsync(ct);
        if (buildingProjectId is null)
        {
            throw new NotFoundException($"Building {request.ImmeubleId} not found.");
        }

        await _projectScope.EnsureProjectAccessAsync(buildingProjectId.Value, ct);

        var existing = await _db.Set<Floor>()
            .Where(f => f.ImmeubleId == request.ImmeubleId)
            .ToListAsync(ct);

        if (existing.Any(f => string.Equals(f.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                $"Un étage nommé « {request.Name} » existe déjà dans ce bâtiment.",
                StatusCodes.Status409Conflict);
        }

        var floor = new Floor
        {
            Id = Guid.NewGuid(),
            ImmeubleId = request.ImmeubleId,
            Name = request.Name,
            SequenceNo = request.SequenceNo ?? existing.Count,
        };

        _db.Add(floor);
        await _db.SaveChangesAsync(ct);

        return new CreateFloorResponse { FloorId = floor.Id, Message = "Étage créé avec succès." };
    }
}
