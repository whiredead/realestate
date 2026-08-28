namespace ProjectAPI.Api.Application.Sales.GetAllSales;

/// <summary>One sold unit, with everything the admin list shows in one row.</summary>
public class AdminSaleItem
{
    public Guid SaleId { get; set; }
    public Guid UnitId { get; set; }

    /// <summary>Buyer as recorded on the sale. May be blank on imported rows.</summary>
    public string BuyerName { get; set; } = string.Empty;
    public string? BuyerEmail { get; set; }
    public string? BuyerPhone { get; set; }

    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ImmeubleName { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;

    public DateTime SaleDate { get; set; }
    public decimal TotalPrice { get; set; }

    /// <summary>Sum of the sale's payment ledger.</summary>
    public decimal Paid { get; set; }

    /// <summary>TotalPrice - Paid, never negative.</summary>
    public decimal Remaining { get; set; }

    /// <summary>Share of the price settled, 0–100, rounded to a whole percent.</summary>
    public int PaidPercent { get; set; }

    /// <summary>Agent credited with the sale, resolved via the owning reservation.</summary>
    public string? AgentName { get; set; }
}

/// <summary>
/// A page of sales plus totals for the whole filtered set — the header figures
/// must describe every matching sale, not just the rows on the current page.
/// </summary>
public class AllSalesResponse
{
    public List<AdminSaleItem> Sales { get; set; } = new();

    public int TotalItems { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }

    public decimal TotalValue { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalRemaining { get; set; }
}
