using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Sales.Interfaces;

namespace ProjectAPI.Api.Application.Sales.ScheduleDelivery;

public class UpdateDeliveryStatusHandler : IRequestHandler<UpdateDeliveryStatusCommand, bool>
{
    private readonly IPropertyDeliveryRepository _repo;

    public UpdateDeliveryStatusHandler(IPropertyDeliveryRepository repo) => _repo = repo;

    public async Task<bool> Handle(UpdateDeliveryStatusCommand r, CancellationToken ct)
    {
        var d = await _repo.GetByIDAsync(r.DeliveryId)
            ?? throw new NotFoundException($"Delivery {r.DeliveryId} not found.");

        d.Status = r.Status;
        d.Report = r.Report;

        await _repo.Update(d);
        await _repo.SaveAsync();
        return true;
    }
}