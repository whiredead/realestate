using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Domain.Sales.Interfaces;

namespace ProjectAPI.Api.Application.Sales.ScheduleDelivery;

public class ScheduleDeliveryHandler : IRequestHandler<ScheduleDeliveryCommand, Guid>
{
    private readonly IPropertyDeliveryRepository _repo;
    private readonly ISaleRepository _saleRepo;
    private readonly ProjectScopeService _projectScope;

    public ScheduleDeliveryHandler(IPropertyDeliveryRepository repo, ISaleRepository saleRepo, ProjectScopeService projectScope)
    {
        _repo = repo;
        _saleRepo = saleRepo;
        _projectScope = projectScope;
    }

    public async Task<Guid> Handle(ScheduleDeliveryCommand r, CancellationToken ct)
    {
        _ = await _saleRepo.GetByIDAsync(r.SaleId)
            ?? throw new NotFoundException($"Sale {r.SaleId} not found.");

        // §6.4 — a SALES_AGENT/PROJECT_ADMIN outside this unit's project must
        // not schedule a delivery against it.
        await _projectScope.EnsureUnitProjectAccessAsync(r.UnitId, ct);

        var d = new PropertyDelivery
        {
            Id = Guid.NewGuid(),
            SaleId = r.SaleId,
            UnitId = r.UnitId,
            DeliveryDate = r.DeliveryDate,
            Status = r.Status,
            Report = r.Report
        };

        await _repo.InsertAsync(d);
        await _repo.SaveAsync();
        return d.Id;
    }
}