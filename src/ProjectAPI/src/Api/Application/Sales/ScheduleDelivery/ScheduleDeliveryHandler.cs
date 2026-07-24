using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Domain.Sales.Interfaces;

namespace ProjectAPI.Api.Application.Sales.ScheduleDelivery;

public class ScheduleDeliveryHandler : IRequestHandler<ScheduleDeliveryCommand, Guid>
{
    private readonly IPropertyDeliveryRepository _repo;
    private readonly ISaleRepository _saleRepo;

    public ScheduleDeliveryHandler(IPropertyDeliveryRepository repo, ISaleRepository saleRepo)
    {
        _repo = repo;
        _saleRepo = saleRepo;
    }

    public async Task<Guid> Handle(ScheduleDeliveryCommand r, CancellationToken ct)
    {
        _ = await _saleRepo.GetByIDAsync(r.SaleId)
            ?? throw new NotFoundException($"Sale {r.SaleId} not found.");

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