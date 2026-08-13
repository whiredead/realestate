using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Units.UpdateUnit;

/// <summary>
/// Handler for updating a unit.
/// </summary>
public class UpdateUnitHandler : IRequestHandler<UpdateUnitCommand, UpdateUnitResponse>
{
    private readonly IUnitRepository _repository;
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public UpdateUnitHandler(IUnitRepository repository, ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _repository = repository;
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<UpdateUnitResponse> Handle(UpdateUnitCommand request, CancellationToken cancellationToken)
    {
        var unit = await _repository.GetByIDAsync(request.Id);
        if (unit == null)
        {
            return new UpdateUnitResponse
            {
                IsSuccess = false,
                Message = "Unit not found."
            };
        }

        // §6.4/§7 — includes price fields (LatestPrice, PriceSaleableValue);
        // without this a PROJECT_ADMIN could reprice any unit in any project.
        // unit.ProjectId is really the FK to Immeuble (Building), so resolve
        // one more hop to the real project id — see Unit.ProjectId doc comment.
        var realProjectId = await _db.Set<Immeuble>()
            .Where(i => i.Id == unit.ProjectId)
            .Select(i => (Guid?)i.ProjectId)
            .FirstOrDefaultAsync(cancellationToken);
        if (realProjectId is null)
        {
            throw new NotFoundException($"Building {unit.ProjectId} not found for unit {unit.Id}.");
        }
        await _projectScope.EnsureProjectAccessAsync(realProjectId.Value, cancellationToken);

        if (request.FloorId.HasValue)
        {
            var floorBelongsToBuilding = await _db.Set<Floor>()
                .AnyAsync(f => f.Id == request.FloorId.Value && f.ImmeubleId == unit.ProjectId, cancellationToken);
            if (!floorBelongsToBuilding)
            {
                throw new NotFoundException("Floor not found for this unit's building.");
            }
        }

        // Dictionary of updates to apply only if the field is filled
        var updateActions = new Dictionary<Func<bool>, Action>
        {
            { () => request.FloorId.HasValue, () => unit.FloorId = request.FloorId!.Value },
            { () => request.UnitNumber != null, () => unit.UnitNumber = request.UnitNumber },
            { () => request.NumberOfBedrooms.HasValue, () => unit.NumberOfBedrooms = request.NumberOfBedrooms },
            { () => request.NumberOfBathrooms.HasValue, () => unit.NumberOfBathrooms = request.NumberOfBathrooms },
            { () => request.ApartmentSurface.HasValue, () => unit.ApartmentSurface = request.ApartmentSurface },
            { () => request.BalconySurface.HasValue, () => unit.BalconySurface = request.BalconySurface },
            { () => request.TerraceSurface.HasValue, () => unit.TerraceSurface = request.TerraceSurface },
            { () => request.GardenSurface.HasValue, () => unit.GardenSurface = request.GardenSurface },
            { () => request.View != null, () => unit.View = request.View },
            { () => request.Orientation != null, () => unit.Orientation = request.Orientation },
            { () => request.TotalSurface.HasValue, () => unit.TotalSurface = request.TotalSurface },
            { () => request.SaleableValue.HasValue, () => unit.SaleableValue = request.SaleableValue },
            { () => request.SaleableValue1.HasValue, () => unit.SaleableValue1 = request.SaleableValue1 },
            { () => request.PriceSaleableValue.HasValue, () => unit.PriceSaleableValue = request.PriceSaleableValue },
            { () => request.PriceSaleableValue1.HasValue, () => unit.PriceSaleableValue1 = request.PriceSaleableValue1 },
            { () => request.LatestPrice.HasValue, () => unit.LatestPrice = request.LatestPrice },
            { () => request.Images != null, () => unit.Images = request.Images }
            // No Status entry: see the note on UpdateUnitCommand (§7).
        };

        // Apply only the updates where the condition is met
        foreach (var updateAction in updateActions)
        {
            if (updateAction.Key.Invoke())
            {
                updateAction.Value.Invoke();
            }
        }

        await _repository.Update(unit);
        await _repository.SaveAsync();

        return new UpdateUnitResponse
        {
            IsSuccess = true,
            Message = "Unit updated successfully."
        };
    }
}
