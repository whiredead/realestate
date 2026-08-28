namespace ProjectAPI.Api.Application.Sales.GetAllSales;

/// <summary>
/// Company-wide sales list for the admin console.
///
/// The only existing read was GetSalesByUser, which answers "what did *I* buy?"
/// — a buyer looking at their own purchases. An administrator opening
/// /admin/sales saw an empty page, because they have never bought a unit. This
/// query answers the question the admin screen is actually asking: what has the
/// company sold, to whom, and how much of it has been paid.
///
/// Scoped by the caller's project perimeter (§6.4) exactly like the dashboard:
/// GLOBAL_ADMIN sees everything, everyone else only their own projects.
/// </summary>
public class GetAllSalesQuery : IRequest<AllSalesResponse>
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;

    /// <summary>Optional inclusive lower bound on SaleDate.</summary>
    public DateTime? From { get; set; }

    /// <summary>Optional exclusive upper bound on SaleDate.</summary>
    public DateTime? To { get; set; }

    /// <summary>Optional free-text match on buyer name or unit number.</summary>
    public string? Search { get; set; }

    /// <summary>Optional filter to one project (Sale -> Unit -> Immeuble -> Project).</summary>
    public Guid? ProjectId { get; set; }
}
