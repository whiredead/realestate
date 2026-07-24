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
    /// The total number of sales for the specified timeframe
    /// (month, quarter, year).
    /// </summary>
    public int SalesThisMonth { get; set; }

    /// <summary>
    /// The total value of sales (or number) for the timeframe.
    /// E.g., sum of prices.
    /// </summary>
    public decimal SalesVolumeThisMonth { get; set; }

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
}
