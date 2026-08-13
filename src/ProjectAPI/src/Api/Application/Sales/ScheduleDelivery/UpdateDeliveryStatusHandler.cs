using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Sales.Interfaces;

namespace ProjectAPI.Api.Application.Sales.ScheduleDelivery;

public class UpdateDeliveryStatusHandler : IRequestHandler<UpdateDeliveryStatusCommand, bool>
{
    private readonly IPropertyDeliveryRepository _repo;
    private readonly ProjectScopeService _projectScope;

    public UpdateDeliveryStatusHandler(IPropertyDeliveryRepository repo, ProjectScopeService projectScope)
    {
        _repo = repo;
        _projectScope = projectScope;
    }

    public async Task<bool> Handle(UpdateDeliveryStatusCommand r, CancellationToken ct)
    {
        var d = await _repo.GetByIDAsync(r.DeliveryId)
            ?? throw new NotFoundException($"Delivery {r.DeliveryId} not found.");

        // §6.4 — a SALES_AGENT/PROJECT_ADMIN outside this delivery's unit's
        // project must not mutate its status/report.
        await _projectScope.EnsureUnitProjectAccessAsync(d.UnitId, ct);

        d.Status = r.Status;
        d.Report = r.Report;

        await _repo.Update(d);
        await _repo.SaveAsync();
        return true;
    }
}