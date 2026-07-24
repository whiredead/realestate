namespace ProjectAPI.Api.Application.AdminDashboards.Dashboard;

public class AdminDashboardQuery : IRequest<AdminDashboardResponse>
{
    /// <summary>
    /// Optional: filter by month, year, etc.
    /// For example, we can pass a Year or Month if we want to see data for a given period.
    /// </summary>
    public int? Year { get; set; }
    public int? Month { get; set; }

    /// <summary>
    /// Could also have a Quarter, or a time range
    /// (StartDate, EndDate), etc. depending on your needs.
    /// </summary>
    // public int? Quarter { get; set; }
}