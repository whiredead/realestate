namespace ProjectAPI.Api.Application.AdminDashboards.Dashboard;
public class AdminDashboardResponse
{
    /// <summary>
    /// Total number of agents in the system.
    /// </summary>
    public int TotalAgents { get; set; }

    /// <summary>
    /// Number (or list) of projects assigned (or total projects).
    /// </summary>
    public int TotalProjects { get; set; }

    /// <summary>
    /// Construction progress in %, weighted by milestone weights (completed
    /// milestones / total weight), averaged over the projects in scope. A
    /// project with no milestone counts with its stored OverAllProgress.
    /// </summary>
    public double ConstructionProgressPct { get; set; }

    /// <summary>Current stock in scope, by commercial state.</summary>
    public int TotalUnits { get; set; }
    public int AvailableUnits { get; set; }
    /// <summary>Held pending approval, reserved or contracted.</summary>
    public int ReservedUnits { get; set; }
    /// <summary>Sold, not yet handed over.</summary>
    public int SoldUnits { get; set; }
    public int DeliveredUnits { get; set; }

    /// <summary>
    /// The total number of sales for the specified timeframe
    /// (month, quarter, year, or explicit range).
    /// </summary>
    public int SalesThisMonth { get; set; }

    /// <summary>
    /// The total value of sales (or number) for the timeframe.
    /// E.g., sum of prices.
    /// </summary>
    public decimal SalesVolumeThisMonth { get; set; }

    /// <summary>
    /// Live reservations created in the selected reporting period (submitted,
    /// changes requested or approved).
    /// </summary>
    public int ReservationsInPeriod { get; set; }

    /// <summary>
    /// Sum of the reservation amounts declared on live reservations created
    /// in the selected reporting period.
    /// </summary>
    public decimal ReservationAmountInPeriod { get; set; }

    /// <summary>
    /// Current reservation rate: live reservations (pending, changes
    /// requested or approved) divided by all units in scope.
    /// It is deliberately a point-in-time inventory metric, not a period
    /// total, and is returned as a percentage in the 0-100 range.
    /// </summary>
    public double ReservationRatePct { get; set; }

    /// <summary>
    /// Mean reservation amount for reservations created in the selected
    /// reporting period. Zero when the period contains no reservations.
    /// </summary>
    public decimal AverageReservationAmountInPeriod { get; set; }

    /// <summary>
    /// A dictionary or list of performance stats for each agent,
    /// possibly aggregated by time period.
    /// Key = AgentId / AgentName,
    /// Value = Some aggregated data (like number of sales).
    /// </summary>
    public List<AgentPerformanceDto> AgentsPerformance { get; set; } = new();

    /// <summary>
    /// List of top performers based on some KPI
    /// (like highest sales, best conversion rate, etc.).
    /// </summary>
    public List<AgentPerformanceDto> TopPerformers { get; set; } = new();

    /// <summary>
    /// The resolved period this response actually covers (echoes back
    /// whatever Year/Month/StartDate/EndDate combination the request
    /// resolved to), so the frontend can render an unambiguous "which period
    /// is active" indicator without re-deriving the same date math.
    /// </summary>
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    /// <summary>
    /// Same figures computed for the immediately preceding period of equal
    /// length (e.g. this month vs last month), so the frontend can show a
    /// %-change without a second round trip.
    /// </summary>
    public PeriodComparisonDto? PreviousPeriod { get; set; }

    /// <summary>Sales velocity (units sold per month) per project over the selected range, trending.</summary>
    public List<ProjectVelocityDto> SalesVelocityByProject { get; set; } = new();

    /// <summary>Occupancy/sell-through per project (% sold vs total units).</summary>
    public List<ProjectSellThroughDto> TopPerformingProjects { get; set; } = new();
    public List<ProjectSellThroughDto> BottomPerformingProjects { get; set; } = new();

    /// <summary>Immeubles at or above the near-sellout threshold (default 90%).</summary>
    public List<ImmeubleNearSelloutDto> NearSelloutBuildings { get; set; } = new();

    /// <summary>Global inventory snapshot broken down per project, for a visual (not just one number) sense of what's left.</summary>
    public List<ProjectInventoryDto> InventoryByProject { get; set; } = new();

    /// <summary>Live equivalent of the commercial workbook's Synthèse tab.</summary>
    public PricingSynthesisDto PricingSynthesis { get; set; } = new();
}

public class PricingSynthesisDto
{
    public decimal RemainingStockValue { get; set; }
    public decimal ContractedSalesValue { get; set; }
    public decimal TotalCommercialValue { get; set; }
    public int TotalStock { get; set; }
    public int EngagedStock { get; set; }
    public int RemainingStock { get; set; }
    public int PendingApprovalStock { get; set; }
    public double EngagedStockPct { get; set; }
    public double ContractedValuePct { get; set; }
    public double PendingOfRemainingPct { get; set; }
    public double PendingOfTotalPct { get; set; }
    public List<ImmeublePricingSynthesisDto> Buildings { get; set; } = new();
}

public class ImmeublePricingSynthesisDto
{
    public Guid ImmeubleId { get; set; }
    public string ImmeubleName { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public int TotalUnits { get; set; }
    public int EngagedOrSoldUnits { get; set; }
    public int RemainingUnits { get; set; }
    public double RemainingPct { get; set; }
    public decimal RemainingStockValue { get; set; }
}

public class PeriodComparisonDto
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int SalesCount { get; set; }
    public decimal SalesVolume { get; set; }

    /// <summary>Null when the previous period had zero sales (a % change against zero is undefined, not infinite).</summary>
    public double? SalesCountChangePct { get; set; }
    public double? SalesVolumeChangePct { get; set; }
}

/// <summary>Units sold per month for one project across the selected range — the trend line the dashboard plots.</summary>
public class ProjectVelocityDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public List<MonthlyVelocityPointDto> Points { get; set; } = new();

    /// <summary>Average units/month across the selected range — the single figure a table column can show.</summary>
    public double AverageUnitsPerMonth { get; set; }
}

public class MonthlyVelocityPointDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int UnitsSold { get; set; }
}

public class ProjectSellThroughDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public int TotalUnits { get; set; }
    public int SoldUnits { get; set; }
    public double SellThroughPct { get; set; }

    /// <summary>Units sold within the selected date range only (drives the top/bottom ranking, distinct from all-time SellThroughPct).</summary>
    public int UnitsSoldInPeriod { get; set; }
}

public class ImmeubleNearSelloutDto
{
    public Guid ImmeubleId { get; set; }
    public string ImmeubleName { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public int TotalUnits { get; set; }
    public int SoldUnits { get; set; }
    public double SellThroughPct { get; set; }
    public int RemainingUnits { get; set; }
}

/// <summary>Per-project inventory breakdown for the "visual sense of what's left" requirement.</summary>
public class ProjectInventoryDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public int TotalUnits { get; set; }
    public int AvailableUnits { get; set; }
    public int ReservedUnits { get; set; }
    public int SoldUnits { get; set; }
}
