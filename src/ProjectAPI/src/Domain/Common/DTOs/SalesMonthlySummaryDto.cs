
namespace ProjectAPI.Domain.Common.DTOs;

public class SalesMonthlySummaryDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int CountSales { get; set; }
    public decimal TotalRevenue { get; set; }
}
