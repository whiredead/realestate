using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Units.Pricing;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
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

        // §7.2 — same rule as on create: a unit may only claim a layout its own
        // project offers. 0 is the "clear it" sentinel and skips the check.
        if (request.TypeBienId is int typeBienId && typeBienId != 0)
        {
            var typeOfferedByProject = await _db.Set<ProjectTypeBien>()
                .AnyAsync(
                    link => link.ProjectId == realProjectId.Value && link.TypeBienId == typeBienId,
                    cancellationToken);

            if (!typeOfferedByProject)
            {
                throw new NotFoundException("Type de bien not offered by this unit's project.");
            }
        }

        // Non-commercial fields may be patched independently.
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
            { () => request.Images != null, () => unit.Images = request.Images },
            // 0 means "clear the type"; any other value sets it. Null, like
            // every other field here, leaves the current value untouched.
            { () => request.TypeBienId.HasValue,
              () => unit.TypeBienId = request.TypeBienId == 0 ? null : request.TypeBienId }
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

        // TotalSurface, SV, SV1 and the two derived price rates are never
        // patched independently. They are formula columns, recalculated from
        // the same editable values used by the Excel workbook. A price supplied
        // in this request wins; otherwise keep the existing final price while
        // recalculating it against any changed surfaces.
        var pricing = UnitPricingCalculator.Calculate(new UnitPricingInput(
            unit.ApartmentSurface,
            unit.BalconySurface,
            unit.TerraceSurface,
            unit.GardenSurface,
            request.LatestPrice.HasValue ? null : request.PriceSaleableValue ?? unit.PriceSaleableValue,
            request.LatestPrice.HasValue || request.PriceSaleableValue.HasValue ? null : request.PriceSaleableValue1 ?? unit.PriceSaleableValue1,
            request.LatestPrice ?? (request.PriceSaleableValue.HasValue || request.PriceSaleableValue1.HasValue ? null : unit.LatestPrice)));

        unit.TotalSurface = pricing.TotalSurface;
        unit.SaleableValue = pricing.SaleableValue;
        unit.SaleableValue1 = pricing.SaleableValue1;
        unit.PriceSaleableValue = pricing.PriceSaleableValue;
        unit.PriceSaleableValue1 = pricing.PriceSaleableValue1;
        unit.LatestPrice = pricing.LatestPrice;

        await _repository.Update(unit);
        await _repository.SaveAsync();

        return new UpdateUnitResponse
        {
            IsSuccess = true,
            Message = "Unit updated successfully."
        };
    }
}
