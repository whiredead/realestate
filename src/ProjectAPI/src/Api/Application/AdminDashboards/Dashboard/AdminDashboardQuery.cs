namespace ProjectAPI.Api.Application.AdminDashboards.Dashboard;

public class AdminDashboardQuery : IRequest<AdminDashboardResponse>
{
    /// <summary>
    /// Legacy filters, kept for backward compatibility with any existing
    /// caller: filter to a single calendar month/year. Ignored once
    /// <see cref="StartDate"/>/<see cref="EndDate"/> are supplied.
    /// </summary>
    public int? Year { get; set; }
    public int? Month { get; set; }

    /// <summary>
    /// Explicit date range (inclusive of StartDate, exclusive of the instant
    /// after EndDate's calendar day — the handler treats EndDate as "through
    /// end of that day"). Takes priority over Year/Month when present. Both
    /// must be supplied together.
    /// </summary>
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    /// <summary>Narrows every figure to one project (always within the caller's own perimeter).</summary>
    public Guid? ProjectId { get; set; }
}
