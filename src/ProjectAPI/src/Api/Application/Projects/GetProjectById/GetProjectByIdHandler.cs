using ProjectAPI.Domain.Construction.Entities;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Infrastructure.Context;
using UnitEntity = ProjectAPI.Domain.Immeubles.Entities.Unit;

namespace ProjectAPI.Api.Application.Projects.GetProjectById;

/// <summary>
/// §6.4 — same perimeter rule as every other project-scoped read: a caller
/// below GLOBAL_ADMIN must be a member of this specific project, not just
/// authenticated. Stock figures are computed live from Unit.Status, same
/// principle as GetImmeubleByIdHandler/GetAllImmeublesHandler — nothing here
/// reads a stored/denormalized counter.
/// </summary>
public class GetProjectByIdHandler : IRequestHandler<GetProjectByIdQuery, ProjectDrillDownResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public GetProjectByIdHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<ProjectDrillDownResponse> Handle(GetProjectByIdQuery request, CancellationToken ct)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == request.Id, ct)
            ?? throw new NotFoundException($"Project {request.Id} not found.");

        await _projectScope.EnsureProjectAccessAsync(request.Id, ct);

        var immeubles = await _db.Set<Immeuble>()
            .Where(im => im.ProjectId == request.Id)
            .Select(im => new { im.Id, im.Name, im.Location, im.Status, im.ImagePrincipale })
            .ToListAsync(ct);

        var immeubleIds = immeubles.Select(im => im.Id).ToList();

        var units = await _db.Set<UnitEntity>()
            .Where(u => immeubleIds.Contains(u.ProjectId))
            .Select(u => new { u.Id, u.ProjectId, u.Status })
            .ToListAsync(ct);

        var floorCounts = await _db.Set<Floor>()
            .Where(f => immeubleIds.Contains(f.ImmeubleId))
            .GroupBy(f => f.ImmeubleId)
            .Select(g => new { ImmeubleId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var floorCountByImmeuble = floorCounts.ToDictionary(f => f.ImmeubleId, f => f.Count);

        var unitIds = units.Select(u => u.Id).ToList();
        var recentCutoff = DateTime.UtcNow.AddDays(-90);
        var recentSales = await _db.Set<Sale>()
            .Where(s => unitIds.Contains(s.UnitId) && s.SaleDate >= recentCutoff)
            .Join(_db.Set<UnitEntity>(), s => s.UnitId, u => u.Id, (s, u) => new { u.ProjectId })
            .ToListAsync(ct);
        var recentSalesByImmeuble = recentSales
            .GroupBy(s => s.ProjectId)
            .ToDictionary(g => g.Key, g => g.Count());

        var immeubleDtos = new List<ImmeubleDrillDownDto>();
        foreach (var im in immeubles)
        {
            var imUnits = units.Where(u => u.ProjectId == im.Id).ToList();
            var total = imUnits.Count;
            var available = imUnits.Count(u => u.Status == UnitCommercialStatus.Available);
            var reserved = imUnits.Count(u => u.Status is UnitCommercialStatus.HoldPendingApproval or UnitCommercialStatus.Reserved or UnitCommercialStatus.Contracted);
            var sold = imUnits.Count(u => u.Status is UnitCommercialStatus.Sold or UnitCommercialStatus.Delivered);
            var recentSold = recentSalesByImmeuble.GetValueOrDefault(im.Id, 0);

            immeubleDtos.Add(new ImmeubleDrillDownDto
            {
                Id = im.Id,
                Name = im.Name,
                Location = im.Location,
                Status = ProjectStatusCodes.Normalize(im.Status),
                ImagePrincipale = im.ImagePrincipale,
                TotalUnits = total,
                AvailableUnits = available,
                ReservedUnits = reserved,
                SoldUnits = sold,
                SellThroughPct = total > 0 ? Math.Round(sold * 100.0 / total, 1) : 0,
                RecentUnitsPerMonth = Math.Round(recentSold / 3.0, 2),
                FloorCount = floorCountByImmeuble.GetValueOrDefault(im.Id, 0),
            });
        }

        var allUnitsTotal = units.Count;
        var allUnitsAvailable = units.Count(u => u.Status == UnitCommercialStatus.Available);
        var allUnitsReserved = units.Count(u => u.Status is UnitCommercialStatus.HoldPendingApproval or UnitCommercialStatus.Reserved or UnitCommercialStatus.Contracted);
        var allUnitsSold = units.Count(u => u.Status is UnitCommercialStatus.Sold or UnitCommercialStatus.Delivered);

        return new ProjectDrillDownResponse
        {
            Id = project.Id,
            Name = project.Name,
            Location = project.Location,
            Address = project.Address,
            Type = project.Type,
            StatusGlobal = project.StatusGlobal,
            StatusReferenceCode = project.StatusReferenceCode,
            OverAllProgress = project.OverAllProgress,
            Images = project.Images ?? new List<string>(),
            TotalUnits = allUnitsTotal,
            AvailableUnits = allUnitsAvailable,
            ReservedUnits = allUnitsReserved,
            SoldUnits = allUnitsSold,
            SellThroughPct = allUnitsTotal > 0 ? Math.Round(allUnitsSold * 100.0 / allUnitsTotal, 1) : 0,
            Immeubles = immeubleDtos.OrderBy(im => im.Name).ToList(),
        };
    }
}
