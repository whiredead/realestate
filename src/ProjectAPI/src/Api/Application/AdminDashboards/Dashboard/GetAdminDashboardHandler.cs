using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;
using UnitEntity = ProjectAPI.Domain.Immeubles.Entities.Unit;

namespace ProjectAPI.Api.Application.AdminDashboards.Dashboard;

/// <summary>
/// §6.4 — every figure here used to be computed platform-wide with zero
/// project filtering, even though the route admits PROJECT_ADMIN
/// (`[Authorize(Roles = RoleGroups.Admins)]`): total project count, sales
/// count/volume across every project, and agent performance/PII for agents
/// on projects the caller has no membership on. GLOBAL_ADMIN keeps seeing
/// everything (GetScopedProjectIdsAsync returns null for them); everyone
/// else is now intersected with their own scoped project ids.
///
/// Date-range filtering: the handler resolves whatever combination of
/// Year/Month/StartDate/EndDate the request carries into one concrete
/// [PeriodStart, PeriodEnd) window (StartDate/EndDate win when both are
/// supplied), computes every figure against that window, then repeats the
/// same computation for the immediately preceding window of equal length so
/// the frontend can render a %-change without a second round trip.
/// </summary>
public class GetAdminDashboardHandler : IRequestHandler<AdminDashboardQuery, AdminDashboardResponse>
{
    private readonly ApplicationDbContext _context;
    private readonly ProjectScopeService _projectScope;

    /// <summary>A building at or above this sell-through is "nearing full sell-out" and worth surfacing for restocking/new-phase decisions.</summary>
    private const double NearSelloutThresholdPct = 90.0;

    public GetAdminDashboardHandler(ApplicationDbContext context, ProjectScopeService projectScope)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _projectScope = projectScope;
    }

    public async Task<AdminDashboardResponse> Handle(AdminDashboardQuery request, CancellationToken cancellationToken)
    {
        var response = new AdminDashboardResponse();

        var (periodStart, periodEnd) = ResolvePeriod(request);
        response.PeriodStart = periodStart;
        response.PeriodEnd = periodEnd;

        // null (GLOBAL_ADMIN) means unrestricted; otherwise the caller's own
        // scoped project ids, used to filter every figure below.
        var scopedProjectIds = await _projectScope.GetScopedProjectIdsAsync(cancellationToken);

        // A project filter narrows the perimeter; it never widens it.
        if (request.ProjectId.HasValue)
        {
            scopedProjectIds = scopedProjectIds is null || scopedProjectIds.Contains(request.ProjectId.Value)
                ? new List<Guid> { request.ProjectId.Value }
                : new List<Guid>();
        }

        // Agents/units/sales resolve to a project via unit -> immeuble ->
        // project (Unit.ProjectId is actually the FK to Immeuble — see
        // Unit.cs doc comment); membership resolves an agent to a project
        // directly.
        var scopedUnitIds = scopedProjectIds is null
            ? null
            : (await _context.Set<UnitEntity>()
                .Join(_context.Set<Immeuble>(), u => u.ProjectId, im => im.Id, (u, im) => new { u.Id, im.ProjectId })
                .Where(x => scopedProjectIds.Contains(x.ProjectId))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken));

        var scopedAgentIds = scopedProjectIds is null
            ? null
            : (await _context.Set<ProjectMembership>()
                .Where(m => scopedProjectIds.Contains(m.ProjectId) && m.IsActive)
                .Select(m => m.UserId)
                .Distinct()
                .ToListAsync(cancellationToken));

        // 1) Count total agents (scoped to those with an active membership on
        //    one of the caller's own projects).
        response.TotalAgents = await _context.Users
            .CountAsync(u => u is Agent && (scopedAgentIds == null || scopedAgentIds.Contains(u.Id)), cancellationToken);

        // 2) Count total projects (scoped to the caller's own perimeter).
        response.TotalProjects = scopedProjectIds is null
            ? await _context.Projects.CountAsync(cancellationToken)
            : await _context.Projects.CountAsync(p => scopedProjectIds.Contains(p.Id), cancellationToken);

        // 2a) Construction progress and current stock by state.
        await PopulateStockAndProgressAsync(response, scopedProjectIds, cancellationToken);

        // 3) Sales for the selected period, scoped to units within the
        //    caller's own perimeter.
        var salesInPeriod = await SalesQuery(scopedUnitIds, periodStart, periodEnd).ToListAsync(cancellationToken);
        response.SalesThisMonth = salesInPeriod.Count;
        response.SalesVolumeThisMonth = salesInPeriod.Sum(s => s.TotalPrice);

        // 3a) Reservation KPIs. The period metrics use CreatedAt, the same
        // immutable event timestamp used for other reporting data. The rate
        // is intentionally current stock occupancy, so a manager can see how
        // much of their scoped inventory is presently tied to a live dossier.
        var reservationsInPeriodQuery = _context.Set<Reservation>()
            .Where(r => r.CreatedAt >= periodStart
                && r.CreatedAt < periodEnd
                && (r.Status == ReservationStatus.Pending
                    || r.Status == ReservationStatus.ChangesRequested
                    || r.Status == ReservationStatus.Approved));
        if (scopedUnitIds is not null)
        {
            reservationsInPeriodQuery = reservationsInPeriodQuery.Where(r => scopedUnitIds.Contains(r.UnitId));
        }

        var reservationPeriodMetrics = await reservationsInPeriodQuery
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Amount = g.Sum(r => r.ReservationAmount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        response.ReservationsInPeriod = reservationPeriodMetrics?.Count ?? 0;
        response.ReservationAmountInPeriod = reservationPeriodMetrics?.Amount ?? 0;
        response.AverageReservationAmountInPeriod = response.ReservationsInPeriod == 0
            ? 0
            : Math.Round(response.ReservationAmountInPeriod / response.ReservationsInPeriod, 2);

        var liveStatuses = new[]
{
    ReservationStatus.Pending,
    ReservationStatus.ChangesRequested,
    ReservationStatus.Approved
};

        var liveReservationsQuery = _context.Set<Reservation>()
            .Where(r => liveStatuses.Contains(r.Status));

        if (scopedUnitIds is not null)
        {
            liveReservationsQuery = liveReservationsQuery.Where(r => scopedUnitIds.Contains(r.UnitId));
        }

        var liveReservedUnitCount = await liveReservationsQuery
            .Select(r => r.UnitId)
            .Distinct()
            .CountAsync(cancellationToken);
        var scopedUnitCount = scopedUnitIds is null
            ? await _context.Set<UnitEntity>().CountAsync(cancellationToken)
            : scopedUnitIds.Count;
        response.ReservationRatePct = scopedUnitCount == 0
            ? 0
            : Math.Round(liveReservedUnitCount * 100.0 / scopedUnitCount, 1);

        // 3b) Comparison to the immediately preceding period of equal length.
        var periodLength = periodEnd - periodStart;
        var previousStart = periodStart - periodLength;
        var previousEnd = periodStart;
        var salesInPreviousPeriod = await SalesQuery(scopedUnitIds, previousStart, previousEnd).ToListAsync(cancellationToken);
        var prevCount = salesInPreviousPeriod.Count;
        var prevVolume = salesInPreviousPeriod.Sum(s => s.TotalPrice);
        response.PreviousPeriod = new PeriodComparisonDto
        {
            PeriodStart = previousStart,
            PeriodEnd = previousEnd,
            SalesCount = prevCount,
            SalesVolume = prevVolume,
            SalesCountChangePct = PctChange(prevCount, response.SalesThisMonth),
            SalesVolumeChangePct = PctChange((double)prevVolume, (double)response.SalesVolumeThisMonth),
        };

        // 4) Performance Indicators per agent, for the same selected period.
        //    Leads/appointments still come from PerformanceIndicator (the
        //    only thing that writes to it is CreateAppointmentHandler).
        //    SuccessfulSales/SalesVolume used to come from the same table's
        //    SuccessfulSales column, but nothing in the codebase ever
        //    increments it (no IncrementSuccessfulSales call site exists),
        //    so both tables showed "Aucune donnée" even with a real,
        //    correctly-scoped completed sale sitting in Sale/the KPI tiles
        //    above. Sales are now aggregated directly from Sale, joined to
        //    the owning agent via Reservation.OwnerSalesAgentId (frozen at
        //    submit, §5.3) — the same source of truth SalesThisMonth/
        //    SalesVolumeThisMonth already use.
        var perfQuery = _context.Set<PerformanceIndicator>()
            .Where(pi => pi.RecordedAt >= periodStart && pi.RecordedAt < periodEnd);
        if (scopedAgentIds is not null)
        {
            perfQuery = perfQuery.Where(pi => pi.AgentId != null && scopedAgentIds.Contains(pi.AgentId));
        }

        var perfData = await perfQuery
            .GroupBy(pi => pi.AgentId)
            .Select(g => new
            {
                AgentId = g.Key,
                Leads = g.Sum(x => x.LeadsGenerated),
                Appts = g.Sum(x => x.AppointmentsScheduled)
            })
            .ToListAsync(cancellationToken);

        // A unit can carry more than one reservation (an earlier one that
        // lapsed or was cancelled, plus the one that actually converted).
        // Joining Sale -> Reservation on UnitId therefore emitted one row per
        // reservation, so a single sale was credited to every agent who ever
        // held that unit: the per-agent rows summed to more than the headline
        // total on the same screen. Resolve exactly one owning agent per unit
        // first — the most recent reservation wins, matching "frozen at
        // submit" (§5.3) for the reservation that carried the sale through.
        var periodSales = await SalesQuery(scopedUnitIds, periodStart, periodEnd)
            .Select(s => new { s.UnitId, s.TotalPrice })
            .ToListAsync(cancellationToken);

        var soldUnitIds = periodSales.Select(s => s.UnitId).Distinct().ToList();

        var ownerByUnit = (await _context.Set<Reservation>()
                .Where(r => r.OwnerSalesAgentId != null && soldUnitIds.Contains(r.UnitId))
                .Select(r => new { r.UnitId, r.OwnerSalesAgentId, r.CreatedAt })
                .ToListAsync(cancellationToken))
            .GroupBy(r => r.UnitId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => r.CreatedAt).First().OwnerSalesAgentId!);

        var salesByAgent = periodSales
            .Where(s => ownerByUnit.ContainsKey(s.UnitId))
            .GroupBy(s => ownerByUnit[s.UnitId])
            .Select(g => new
            {
                AgentId = g.Key,
                SalesCount = g.Count(),
                SalesVolume = g.Sum(x => x.TotalPrice)
            })
            .ToList();

        // Also gather agent info — union of every agent referenced by either
        // source, so an agent with sales but no logged appointments (or vice
        // versa) still gets a row.
        var agentIds = perfData.Select(x => x.AgentId)
            .Concat(salesByAgent.Select(x => x.AgentId))
            .Distinct()
            .ToList();
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
        foreach (var agentInfo in agents)
        {
            var pd = perfData.FirstOrDefault(x => x.AgentId == agentInfo.Id);
            var sd = salesByAgent.FirstOrDefault(x => x.AgentId == agentInfo.Id);

            var leads = pd?.Leads ?? 0;
            var salesCount = sd?.SalesCount ?? 0;
            var conversionRate = leads == 0 ? 0 : (salesCount / (double)leads) * 100;

            agentPerformanceList.Add(new AgentPerformanceDto
            {
                AgentId = agentInfo.Id,
                AgentFullName = $"{agentInfo.FirstName} {agentInfo.LastName}",
                LeadsGenerated = leads,
                AppointmentsScheduled = pd?.Appts ?? 0,
                SuccessfulSales = salesCount,
                ConversionRate = conversionRate,
                SalesCount = salesCount,
                SalesVolume = sd?.SalesVolume ?? 0
            });
        }

        response.AgentsPerformance = agentPerformanceList
            .OrderByDescending(a => a.SuccessfulSales)
            .ToList();

        // 5) Identify Top Performers
        //    e.g., top 3 based on highest successful sales or best conversion
        response.TopPerformers = agentPerformanceList
            .OrderByDescending(ap => ap.SuccessfulSales)
            .Take(3)
            .ToList();

        // 6) Sales velocity, sell-through, top/bottom projects, near-sellout
        //    buildings and the per-project inventory snapshot — all derived
        //    from the same scoped project/unit/sale data already resolved
        //    above, computed fresh (nothing here is a stored counter).
        await PopulateProjectLevelAggregatesAsync(response, scopedProjectIds, scopedUnitIds, periodStart, periodEnd, cancellationToken);

        return response;
    }

    private async Task PopulateStockAndProgressAsync(AdminDashboardResponse response, List<Guid>? scopedProjectIds, CancellationToken ct)
    {
        var projects = _context.Projects.AsNoTracking();
        if (scopedProjectIds is not null) projects = projects.Where(p => scopedProjectIds.Contains(p.Id));
        var projectRows = await projects.Select(p => new { p.Id, p.OverAllProgress }).ToListAsync(ct);
        var ids = projectRows.Select(p => p.Id).ToList();

        var milestones = await _context.Set<ProjectAPI.Domain.Construction.Entities.ConstructionMilestone>().AsNoTracking()
            .Where(m => ids.Contains(m.ProjectId))
            .Select(m => new { m.ProjectId, m.WeightPercent, m.Status })
            .ToListAsync(ct);
        var byProject = milestones.GroupBy(m => m.ProjectId).ToDictionary(g => g.Key, g => g.ToList());

        var progress = projectRows.Select(p =>
        {
            if (!byProject.TryGetValue(p.Id, out var ms) || ms.Sum(m => m.WeightPercent) <= 0) return (double)p.OverAllProgress;
            var total = ms.Sum(m => m.WeightPercent);
            var done = ms.Where(m => m.Status == ProjectAPI.Domain.Construction.Entities.MilestoneStatus.Completed).Sum(m => m.WeightPercent);
            return (double)(done * 100m / total);
        }).ToList();
        response.ConstructionProgressPct = progress.Count == 0 ? 0 : Math.Round(progress.Average(), 1);

        var statuses = await _context.Set<UnitEntity>().AsNoTracking()
            .Join(_context.Set<Immeuble>(), u => u.ProjectId, im => im.Id, (u, im) => new { u.Status, im.ProjectId })
            .Where(x => ids.Contains(x.ProjectId))
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        int Count(params UnitCommercialStatus[] s) => statuses.Where(x => s.Contains(x.Status)).Sum(x => x.Count);

        response.TotalUnits = statuses.Sum(x => x.Count);
        response.AvailableUnits = Count(UnitCommercialStatus.Available);
        response.ReservedUnits = Count(UnitCommercialStatus.HoldPendingApproval, UnitCommercialStatus.Reserved, UnitCommercialStatus.Contracted);
        response.SoldUnits = Count(UnitCommercialStatus.Sold);
        response.DeliveredUnits = Count(UnitCommercialStatus.Delivered);
    }

    private IQueryable<Sale> SalesQuery(List<Guid>? scopedUnitIds, DateTime start, DateTime end)
    {
        var query = _context.Set<Sale>().Where(s => s.SaleDate >= start && s.SaleDate < end);
        if (scopedUnitIds is not null)
        {
            query = query.Where(s => scopedUnitIds.Contains(s.UnitId));
        }
        return query;
    }

    /// <summary>
    /// Resolves Year/Month/StartDate/EndDate into one concrete [start, end)
    /// window. StartDate/EndDate win when both are supplied (arbitrary custom
    /// range); otherwise Year/Month narrow to that calendar month, Year alone
    /// narrows to that calendar year, and no filter at all defaults to the
    /// current calendar month — the same default the frontend already relied
    /// on implicitly.
    /// </summary>
    private static (DateTime Start, DateTime End) ResolvePeriod(AdminDashboardQuery request)
    {
        if (request.StartDate.HasValue && request.EndDate.HasValue)
        {
            var start = request.StartDate.Value.Date;
            // EndDate is inclusive of that calendar day from the caller's
            // point of view, so the window's exclusive upper bound is the
            // day after.
            var end = request.EndDate.Value.Date.AddDays(1);
            return (start, end);
        }

        var now = DateTime.UtcNow;
        var year = request.Year ?? now.Year;

        if (request.Month.HasValue)
        {
            var start = new DateTime(year, request.Month.Value, 1, 0, 0, 0, DateTimeKind.Utc);
            return (start, start.AddMonths(1));
        }

        if (request.Year.HasValue)
        {
            var start = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (start, start.AddYears(1));
        }

        // Default: the current calendar month — matches the old hardcoded
        // Year/Month-from-now behaviour exactly, so an unfiltered call keeps
        // returning what it always returned.
        var defaultStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (defaultStart, defaultStart.AddMonths(1));
    }

    private static double? PctChange(double previous, double current)
    {
        if (previous == 0) return current == 0 ? 0 : null;
        return (current - previous) / previous * 100.0;
    }

    /// <summary>
    /// Everything computed at project/immeuble granularity: sales velocity
    /// (units sold per month, trended across the selected range), sell-
    /// through/occupancy per project, top and bottom performing projects (by
    /// units sold within the period), buildings nearing full sell-out, and a
    /// per-project inventory snapshot. All derived live from Unit.Status and
    /// Sale.SaleDate — no stored/denormalized counters — same principle as
    /// GetImmeubleByIdHandler/GetAllImmeublesHandler's live recompute.
    /// </summary>
    private async Task PopulateProjectLevelAggregatesAsync(
        AdminDashboardResponse response,
        List<Guid>? scopedProjectIds,
        List<Guid>? scopedUnitIds,
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken ct)
    {
        var projectsQuery = _context.Projects.AsQueryable();
        if (scopedProjectIds is not null)
        {
            projectsQuery = projectsQuery.Where(p => scopedProjectIds.Contains(p.Id));
        }
        var projects = await projectsQuery
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(ct);

        if (projects.Count == 0) return;

        var projectIdSet = projects.Select(p => p.Id).ToList(); // List: parameterized by EF, not inlined

        // Every unit in scope, with its immeuble and project — one query,
        // reused for inventory, sell-through and near-sellout below.
        var unitsQuery =
            from u in _context.Set<UnitEntity>()
            join im in _context.Set<Immeuble>() on u.ProjectId equals im.Id
            where projectIdSet.Contains(im.ProjectId)
            select new { u.Id, u.Status, ImmeubleId = im.Id, ImmeubleName = im.Name, ProjectId = im.ProjectId };

        var units = await unitsQuery.ToListAsync(ct);

        // Every sale in scope, dated, joined down to project — used for both
        // the all-time sell-through denominator's numerator and the
        // period-scoped velocity/top-bottom ranking.
        var salesQuery =
            from s in _context.Set<Sale>()
            join u in _context.Set<UnitEntity>() on s.UnitId equals u.Id
            join im in _context.Set<Immeuble>() on u.ProjectId equals im.Id
            where projectIdSet.Contains(im.ProjectId)
            select new { s.SaleDate, s.UnitId, ProjectId = im.ProjectId, ProjectName_ = im.Name };

        var sales = await salesQuery.ToListAsync(ct);

        var projectNameById = projects.ToDictionary(p => p.Id, p => p.Name);

        // ---- Inventory + sell-through per project (all-time, i.e. current stock, not period-bound) ----
        var inventoryByProject = new List<ProjectInventoryDto>();
        var sellThroughByProject = new List<ProjectSellThroughDto>();

        foreach (var group in units.GroupBy(u => u.ProjectId))
        {
            var total = group.Count();
            var available = group.Count(u => u.Status == UnitCommercialStatus.Available);
            var reserved = group.Count(u => u.Status is UnitCommercialStatus.HoldPendingApproval or UnitCommercialStatus.Reserved or UnitCommercialStatus.Contracted);
            var sold = group.Count(u => u.Status is UnitCommercialStatus.Sold or UnitCommercialStatus.Delivered);
            var name = projectNameById.GetValueOrDefault(group.Key, "");

            inventoryByProject.Add(new ProjectInventoryDto
            {
                ProjectId = group.Key,
                ProjectName = name,
                TotalUnits = total,
                AvailableUnits = available,
                ReservedUnits = reserved,
                SoldUnits = sold,
            });

            var unitsSoldInPeriod = sales.Count(s => s.ProjectId == group.Key && s.SaleDate >= periodStart && s.SaleDate < periodEnd);

            sellThroughByProject.Add(new ProjectSellThroughDto
            {
                ProjectId = group.Key,
                ProjectName = name,
                TotalUnits = total,
                SoldUnits = sold,
                SellThroughPct = total > 0 ? Math.Round(sold * 100.0 / total, 1) : 0,
                UnitsSoldInPeriod = unitsSoldInPeriod,
            });
        }

        response.InventoryByProject = inventoryByProject.OrderByDescending(p => p.TotalUnits).ToList();

        // Top/bottom performing: ranked by units actually sold within the
        // selected period (a project with 0 sales this period is a
        // legitimate "bottom" entry, not noise to exclude) — sell-through %
        // travels alongside as context, but the ranking itself reflects
        // activity, not standing inventory.
        var ranked = sellThroughByProject.Where(p => p.TotalUnits > 0).ToList();
        response.TopPerformingProjects = ranked
            .OrderByDescending(p => p.UnitsSoldInPeriod)
            .ThenByDescending(p => p.SellThroughPct)
            .Take(5)
            .ToList();
        response.BottomPerformingProjects = ranked
            .OrderBy(p => p.UnitsSoldInPeriod)
            .ThenBy(p => p.SellThroughPct)
            .Take(5)
            .ToList();

        // ---- Near-sellout buildings (per Immeuble, not per Project) ----
        var nearSellout = new List<ImmeubleNearSelloutDto>();
        foreach (var group in units.GroupBy(u => u.ImmeubleId))
        {
            var total = group.Count();
            if (total == 0) continue;
            var sold = group.Count(u => u.Status is UnitCommercialStatus.Sold or UnitCommercialStatus.Delivered);
            var pct = sold * 100.0 / total;
            if (pct < NearSelloutThresholdPct) continue;

            var first = group.First();
            nearSellout.Add(new ImmeubleNearSelloutDto
            {
                ImmeubleId = first.ImmeubleId,
                ImmeubleName = first.ImmeubleName,
                ProjectId = first.ProjectId,
                ProjectName = projectNameById.GetValueOrDefault(first.ProjectId, ""),
                TotalUnits = total,
                SoldUnits = sold,
                SellThroughPct = Math.Round(pct, 1),
                RemainingUnits = total - sold,
            });
        }
        response.NearSelloutBuildings = nearSellout.OrderByDescending(b => b.SellThroughPct).ToList();

        // ---- Sales velocity per project (units sold per month, trended across the selected range) ----
        // Buckets by calendar month within [periodStart, periodEnd) — for a
        // single-month selection this is one point; for a quarter/year/
        // custom range it's the actual trend line the dashboard plots.
        var velocityByProject = new List<ProjectVelocityDto>();
        foreach (var group in sales.Where(s => s.SaleDate >= periodStart && s.SaleDate < periodEnd).GroupBy(s => s.ProjectId))
        {
            var monthly = group
                .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
                .Select(g => new MonthlyVelocityPointDto { Year = g.Key.Year, Month = g.Key.Month, UnitsSold = g.Count() })
                .OrderBy(p => p.Year).ThenBy(p => p.Month)
                .ToList();

            var monthsSpanned = Math.Max(1, MonthsBetween(periodStart, periodEnd));
            var totalSold = monthly.Sum(p => p.UnitsSold);

            velocityByProject.Add(new ProjectVelocityDto
            {
                ProjectId = group.Key,
                ProjectName = projectNameById.GetValueOrDefault(group.Key, ""),
                Points = monthly,
                AverageUnitsPerMonth = Math.Round(totalSold / (double)monthsSpanned, 2),
            });
        }
        response.SalesVelocityByProject = velocityByProject
            .OrderByDescending(v => v.AverageUnitsPerMonth)
            .ToList();
    }

    private static int MonthsBetween(DateTime start, DateTime end)
    {
        var months = (end.Year - start.Year) * 12 + (end.Month - start.Month);
        // A range that doesn't land on exact month boundaries (e.g. a custom
        // 45-day range) still counts as spanning at least the months it
        // touches, rounded up, so "average per month" doesn't divide by 0
        // for a sub-month custom range.
        if (end.Day > start.Day || months == 0) months += 1;
        return Math.Max(1, months);
    }
}
