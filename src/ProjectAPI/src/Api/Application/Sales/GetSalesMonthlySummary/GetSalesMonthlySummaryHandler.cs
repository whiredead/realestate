using ProjectAPI.Domain.Common.DTOs;
using ProjectAPI.Domain.Sales.Interfaces;

namespace ProjectAPI.Api.Application.Sales.GetSalesMonthlySummary;

public class GetSalesMonthlySummaryHandler
    : IRequestHandler<GetSalesMonthlySummaryQuery, List<SalesMonthlySummaryDto>>
{
    private readonly ISaleRepository _repo;
    public GetSalesMonthlySummaryHandler(ISaleRepository repo) => _repo = repo;

    public Task<List<SalesMonthlySummaryDto>> Handle(GetSalesMonthlySummaryQuery r, CancellationToken ct)
        => _repo.GetMonthlySummaryAsync(r.Year);
}