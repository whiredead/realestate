using ProjectAPI.Domain.Immeubles.Interfaces;

namespace ProjectAPI.Api.Application.Units.UpdateUnit;

/// <summary>
/// Handler for updating a unit.
/// </summary>
public class UpdateUnitHandler : IRequestHandler<UpdateUnitCommand, UpdateUnitResponse>
{
    private readonly IUnitRepository _repository;

    public UpdateUnitHandler(IUnitRepository repository)
    {
        _repository = repository;
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

        // Dictionary of updates to apply only if the field is filled
        var updateActions = new Dictionary<Func<bool>, Action>
        {
            { () => request.Floor != null, () => unit.Floor = request.Floor },
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
            { () => request.Status != null, () => unit.Status = request.Status }
        };

        // Apply only the updates where the condition is met
        foreach (var updateAction in updateActions)
        {
            if (updateAction.Key.Invoke())
            {
                updateAction.Value.Invoke();
            }
        }

        _repository.Update(unit);
        await _repository.SaveAsync();

        return new UpdateUnitResponse
        {
            IsSuccess = true,
            Message = "Unit updated successfully."
        };
    }
}
