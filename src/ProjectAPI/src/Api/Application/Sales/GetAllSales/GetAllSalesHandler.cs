using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Payments.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Infrastructure.Context;
using UnitEntity = ProjectAPI.Domain.Immeubles.Entities.Unit;

namespace ProjectAPI.Api.Application.Sales.GetAllSales;

/// <summary>
/// Company-wide sales for the admin console — see <see cref="GetAllSalesQuery"/>
/// for why this exists alongside GetSalesByUser.
/// </summary>
public class GetAllSalesHandler : IRequestHandler<GetAllSalesQuery, AllSalesResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public GetAllSalesHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _projectScope = projectScope ?? throw new ArgumentNullException(nameof(projectScope));
    }

    public async Task<AllSalesResponse> Handle(GetAllSalesQuery request, CancellationToken ct)
    {
        // null means unrestricted (GLOBAL_ADMIN); otherwise the caller's own
        // projects, the same perimeter the dashboard applies.
        var scopedProjectIds = await _projectScope.GetScopedProjectIdsAsync(ct);

        // Sale -> Unit -> Immeuble -> Project. Unit.ProjectId is the FK to
        // Immeuble (see Unit.cs), which is why the join looks one level off.
        var query =
            from s in _db.Set<Sale>()
            join u in _db.Set<UnitEntity>() on s.UnitId equals u.Id
            join im in _db.Set<Immeuble>() on u.ProjectId equals im.Id
            join p in _db.Projects on im.ProjectId equals p.Id
            where scopedProjectIds == null || scopedProjectIds.Contains(im.ProjectId)
            select new
            {
                s.Id,
                s.UnitId,
                s.BuyerFirstName,
                s.BuyerLastName,
                s.BuyerEmail,
                s.BuyerPhoneNumber,
                s.SaleDate,
                s.TotalPrice,
                ProjectId = im.ProjectId,
                ProjectName = p.Name,
                ImmeubleName = im.Name,
                u.UnitNumber,
            };

        if (request.ProjectId.HasValue) query = query.Where(x => x.ProjectId == request.ProjectId.Value);
        if (request.From.HasValue) query = query.Where(x => x.SaleDate >= request.From.Value);
        if (request.To.HasValue) query = query.Where(x => x.SaleDate < request.To.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(x =>
                (x.BuyerFirstName != null && x.BuyerFirstName.Contains(term)) ||
                (x.BuyerLastName != null && x.BuyerLastName.Contains(term)) ||
                (x.UnitNumber != null && x.UnitNumber.Contains(term)) ||
                x.ImmeubleName.Contains(term) ||
                x.ProjectName.Contains(term));
        }

        var rows = await query.OrderByDescending(x => x.SaleDate).ToListAsync(ct);
        var saleIds = rows.Select(x => x.Id).ToList();

        // Collections come from BOTH ledgers, keyed differently.
        //
        // Payment (§14.3) is the current, immutable ledger and hangs off the
        // RESERVATION; PaymentTracking is the retired one that hung off the
        // SALE. Reading only PaymentTracking under-reported collections by
        // roughly 9.7M MAD — it holds 10 rows across 3 projects while Payment
        // holds 31 across 6. Both are summed so historic rows are not lost
        // while the old table still exists.
        //
        // Only Validated (1) counts, per §14.3: PendingValidation, Rejected and
        // Reversed must never appear in a collected total.
        var legacyPaidBySale = await _db.Set<PaymentTracking>()
            .Where(pt => saleIds.Contains(pt.SaleId))
            .GroupBy(pt => pt.SaleId)
            .Select(g => new { SaleId = g.Key, Paid = g.Sum(x => x.AmountPaid) })
            .ToDictionaryAsync(x => x.SaleId, x => x.Paid, ct);

        // Payment -> Reservation -> Unit, so the modern ledger can be folded
        // back onto the sale that shares that unit.
        var soldUnitIds = rows.Select(x => x.UnitId).Distinct().ToList();
        var modernPaidByUnit = await (
                from pay in _db.Set<Payment>()
                join res in _db.Set<Reservation>() on pay.ReservationId equals res.Id
                where soldUnitIds.Contains(res.UnitId) && pay.Status == PaymentStatus.Validated
                group pay by res.UnitId into g
                select new { UnitId = g.Key, Paid = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.UnitId, x => x.Paid, ct);

        // Selling agent comes from the owning reservation. A unit can carry
        // more than one reservation (an earlier one that lapsed), so the most
        // recent wins — the same rule the dashboard uses to avoid crediting a
        // single sale to two agents.
        var unitIds = rows.Select(x => x.UnitId).Distinct().ToList();
        var agentByUnit = (await _db.Set<Reservation>()
                .Where(r => r.OwnerSalesAgentId != null && unitIds.Contains(r.UnitId))
                .Select(r => new { r.UnitId, r.OwnerSalesAgentId, r.CreatedAt })
                .ToListAsync(ct))
            .GroupBy(r => r.UnitId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.CreatedAt).First().OwnerSalesAgentId!);

        var agentIds = agentByUnit.Values.Distinct().ToList();
        var agentNames = await _db.Users
            .Where(u => agentIds.Contains(u.Id))
            .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var items = rows.Select(x =>
        {
            var paid = legacyPaidBySale.GetValueOrDefault(x.Id, 0m)
                     + modernPaidByUnit.GetValueOrDefault(x.UnitId, 0m);
            var remaining = Math.Max(0m, x.TotalPrice - paid);
            var buyer = $"{x.BuyerFirstName} {x.BuyerLastName}".Trim();
            string? agentName = null;
            if (agentByUnit.TryGetValue(x.UnitId, out var agentId))
            {
                agentName = agentNames.GetValueOrDefault(agentId);
            }

            return new AdminSaleItem
            {
                SaleId = x.Id,
                UnitId = x.UnitId,
                BuyerName = buyer,
                BuyerEmail = string.IsNullOrWhiteSpace(x.BuyerEmail) ? null : x.BuyerEmail,
                BuyerPhone = string.IsNullOrWhiteSpace(x.BuyerPhoneNumber) ? null : x.BuyerPhoneNumber,
                ProjectId = x.ProjectId,
                ProjectName = x.ProjectName,
                ImmeubleName = x.ImmeubleName,
                UnitNumber = x.UnitNumber ?? string.Empty,
                SaleDate = x.SaleDate,
                TotalPrice = x.TotalPrice,
                Paid = paid,
                Remaining = remaining,
                PaidPercent = x.TotalPrice > 0
                    ? (int)Math.Round(Math.Min(100m, paid / x.TotalPrice * 100m))
                    : 0,
                AgentName = agentName,
            };
        }).ToList();

        var page = items
            .Skip((Math.Max(1, request.PageNumber) - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new AllSalesResponse
        {
            Sales = page,
            TotalItems = items.Count,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalValue = items.Sum(i => i.TotalPrice),
            TotalPaid = items.Sum(i => i.Paid),
            TotalRemaining = items.Sum(i => i.Remaining),
        };
    }
}
