using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Infrastructure.Context;
using UnitEntity = ProjectAPI.Domain.Immeubles.Entities.Unit;

namespace ProjectAPI.Api.Application.Floors.DeleteFloor;

public class DeleteFloorHandler : IRequestHandler<DeleteFloorCommand, MediatR.Unit>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public DeleteFloorHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<MediatR.Unit> Handle(DeleteFloorCommand request, CancellationToken ct)
    {
        var floor = await _db.Set<Floor>().FirstOrDefaultAsync(f => f.Id == request.Id, ct)
            ?? throw new NotFoundException($"Floor {request.Id} not found.");

        var buildingProjectId = await _db.Set<Immeuble>()
            .Where(i => i.Id == floor.ImmeubleId)
            .Select(i => (Guid?)i.ProjectId)
            .FirstOrDefaultAsync(ct);
        if (buildingProjectId is not null)
        {
            await _projectScope.EnsureProjectAccessAsync(buildingProjectId.Value, ct);
        }

        var unitCount = await _db.Set<UnitEntity>().CountAsync(u => u.FloorId == request.Id, ct);
        if (unitCount > 0)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.InvalidStatusTransition,
                $"Cet étage contient {unitCount} unité(s) : déplacez-les ou supprimez-les avant de supprimer l'étage.",
                StatusCodes.Status409Conflict);
        }

        _db.Remove(floor);
        await _db.SaveChangesAsync(ct);
        return MediatR.Unit.Value;
    }
}
