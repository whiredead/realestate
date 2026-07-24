using ProjectAPI.Domain.Common.DTOs;

namespace ProjectAPI.Api.Application.Sales.GetSalesMonthlySummary;

public class GetSalesMonthlySummaryQuery : IRequest<List<SalesMonthlySummaryDto>>
{
    public int Year { get; set; }
}