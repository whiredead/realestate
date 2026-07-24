using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.AdminDashboards.Dashboard;

public class GetAdminDashboardHandler : IRequestHandler<AdminDashboardQuery, AdminDashboardResponse>
{
    private readonly ApplicationDbContext _context;

    public GetAdminDashboardHandler(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<AdminDashboardResponse> Handle(AdminDashboardQuery request, CancellationToken cancellationToken)
    {
        var response = new AdminDashboardResponse();

        // 1) Count total agents:
        //    - "Agent" is a user with Discriminator == "Agent", or
        //      you might filter by roles in AspNetUserRoles if you're not using TPH.
        response.TotalAgents = await _context.Users
            .CountAsync(u => u is Agent, cancellationToken);

        // 2) Count total projects
        response.TotalProjects = await _context.Projects
            .CountAsync(cancellationToken);

        // 3) Sales for the specified timeframe
        //    If user passes Year/Month, filter by that.
        var salesQuery = _context.Set<Sale>().AsQueryable();
        if (request.Year.HasValue)
        {
            salesQuery = salesQuery
                .Where(s => s.SaleDate.Year == request.Year.Value);
        }
        if (request.Month.HasValue)
        {
            salesQuery = salesQuery
                .Where(s => s.SaleDate.Month == request.Month.Value);
        }

        response.SalesThisMonth = await salesQuery
            .CountAsync(cancellationToken);

        response.SalesVolumeThisMonth = await salesQuery
            .SumAsync(s => s.TotalPrice, cancellationToken);

        // 4) Performance Indicators per agent
        //    You may store monthly/quarterly data in PerformanceIndicator,
        //    or compute on the fly from Sales & Leads.
        //    Example with performance indicators table:
        var currentDate = DateTime.UtcNow;
        var perfQuery = _context.Set<PerformanceIndicator>().AsQueryable();

        // If user wants data for a specific month/year, filter on RecordedAt:
        if (request.Year.HasValue)
        {
            perfQuery = perfQuery
                .Where(pi => pi.RecordedAt.Year == request.Year.Value);
        }
        if (request.Month.HasValue)
        {
            perfQuery = perfQuery
                .Where(pi => pi.RecordedAt.Month == request.Month.Value);
        }

        // We'll do a simple grouping by agent:
        var perfData = await perfQuery
            .GroupBy(pi => pi.AgentId)
            .Select(g => new
            {
                AgentId = g.Key,
                Leads = g.Sum(x => x.LeadsGenerated),
                Appts = g.Sum(x => x.AppointmentsScheduled),
                Sales = g.Sum(x => x.SuccessfulSales)
            })
            .ToListAsync(cancellationToken);

        // Also gather agent info:
        var agentIds = perfData.Select(x => x.AgentId).Distinct().ToList();
        var agents = await _context.Users
            .Where(u => agentIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email
            })
            .ToListAsync(cancellationToken);

        // Build the list of AgentPerformanceDto
        var agentPerformanceList = new List<AgentPerformanceDto>();
        foreach (var pd in perfData)
        {
            var agentInfo = agents.FirstOrDefault(a => a.Id == pd.AgentId);
            if (agentInfo == null) continue;

            // Convert leads/sales to a conversion rate or other metrics as needed
            var conversionRate = pd.Leads == 0 ? 0 : (pd.Sales / (double)pd.Leads) * 100;

            agentPerformanceList.Add(new AgentPerformanceDto
            {
                AgentId = agentInfo.Id,
                AgentFullName = $"{agentInfo.FirstName} {agentInfo.LastName}",
                LeadsGenerated = pd.Leads,
                AppointmentsScheduled = pd.Appts,
                SuccessfulSales = pd.Sales,
                ConversionRate = conversionRate,
                SalesCount = pd.Sales, // Assuming SalesCount is the same as SuccessfulSales
                SalesVolume = 0 // Placeholder, you can calculate this if you have sales data
            });
        }

        response.AgentsPerformance = agentPerformanceList
            .OrderByDescending(a => a.SuccessfulSales)
            .ToList();

        // 5) Identify Top Performers
        //    e.g., top 3 based on highest successful sales or best conversion
        var topPerformers = agentPerformanceList
            .OrderByDescending(ap => ap.SuccessfulSales)
            .Take(3)
            .ToList();

        response.TopPerformers = topPerformers;

        return response;
    }
}