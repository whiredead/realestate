using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Infrastructure.Context;

using UnitEntity = ProjectAPI.Domain.Immeubles.Entities.Unit;

namespace ProjectAPI.Api.Application.Common.Units;

/// <summary>
/// The project, building, floor and unit a business item (reservation, notary
/// appointment, claim, final-visit case, sale) relates to — their own details,
/// not just names — so every screen can show what the item is about.
/// </summary>
public interface IHasUnitLocation
{
    Guid? ProjectId { get; set; }
    string? ProjectName { get; set; }
    Guid? ImmeubleId { get; set; }
    string? ImmeubleName { get; set; }
    string? FloorName { get; set; }
    string? UnitNumber { get; set; }

    /// <summary>Full details of the related project, building, floor and unit.</summary>
    UnitContextDto? UnitContext { get; set; }
}

public class UnitContextDto
{
    public ProjectContextDto Project { get; set; } = new();
    public ImmeubleContextDto Immeuble { get; set; } = new();
    public FloorContextDto? Floor { get; set; }
    public UnitDetailsDto Unit { get; set; } = new();
}

public class ProjectContextDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Address { get; set; }
    public string? StatusGlobal { get; set; }
    /// <summary>Business phase: SUR_PLAN, EN_LIVRAISON, FINALISE, SUSPENDED.</summary>
    public string BusinessPhase { get; set; } = string.Empty;
    public decimal OverAllProgress { get; set; }
    public int WarrantyMonths { get; set; }
}

public class ImmeubleContextDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Type { get; set; }
}

public class FloorContextDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SequenceNo { get; set; }
}

public class UnitDetailsDto
{
    public Guid Id { get; set; }
    public string UnitNumber { get; set; } = string.Empty;
    /// <summary>Commercial status code (AVAILABLE, RESERVED, SOLD, DELIVERED…).</summary>
    public string Status { get; set; } = string.Empty;
    public int? NumberOfBedrooms { get; set; }
    public int? NumberOfBathrooms { get; set; }
    public double? ApartmentSurface { get; set; }
    public double? BalconySurface { get; set; }
    public double? TerraceSurface { get; set; }
    public double? GardenSurface { get; set; }
    public double? TotalSurface { get; set; }
    public double? SaleableValue { get; set; }
    public double? SaleableValue1 { get; set; }
    public string? View { get; set; }
    public string? Orientation { get; set; }
    public decimal? LatestPrice { get; set; }
    public decimal? PriceSaleableValue { get; set; }
    public decimal? PriceSaleableValue1 { get; set; }
}

public static class UnitLocations
{
    /// <summary>One query for any number of units (Unit.ProjectId references the building).</summary>
    public static async Task<Dictionary<Guid, UnitContextDto>> ForUnitsAsync(ApplicationDbContext db, IEnumerable<Guid> unitIds, CancellationToken ct)
    {
        var ids = unitIds.Distinct().ToList();
        if (ids.Count == 0) return new();

        var rows = await (
            from u in db.Set<UnitEntity>()
            join im in db.Set<Immeuble>() on u.ProjectId equals im.Id
            join p in db.Set<Project>() on im.ProjectId equals p.Id
            where ids.Contains(u.Id)
            select new
            {
                u.Id,
                u.UnitNumber,
                u.Status,
                u.NumberOfBedrooms,
                u.NumberOfBathrooms,
                u.ApartmentSurface,
                u.BalconySurface,
                u.TerraceSurface,
                u.GardenSurface,
                u.TotalSurface,
                u.SaleableValue,
                u.SaleableValue1,
                u.View,
                u.Orientation,
                u.LatestPrice,
                u.PriceSaleableValue,
                u.PriceSaleableValue1,
                FloorId = (Guid?)u.Floor.Id,
                FloorName = u.Floor.Name,
                FloorSeq = (int?)u.Floor.SequenceNo,
                ImmeubleId = im.Id,
                ImmeubleName = im.Name,
                ImmeubleLocation = im.Location,
                ImmeubleType = im.Type,
                ProjectId = p.Id,
                ProjectName = p.Name,
                p.Location,
                p.Address,
                p.StatusGlobal,
                p.OverAllProgress,
                p.WarrantyMonths
            }).ToListAsync(ct);

        return rows.ToDictionary(r => r.Id, r => new UnitContextDto
        {
            Project = new ProjectContextDto
            {
                Id = r.ProjectId,
                Name = r.ProjectName,
                Location = r.Location,
                Address = r.Address,
                StatusGlobal = ProjectStatusCodes.Normalize(r.StatusGlobal),
                BusinessPhase = ProjectStatusCodes.GetBusinessPhase(r.StatusGlobal),
                OverAllProgress = r.OverAllProgress,
                WarrantyMonths = r.WarrantyMonths
            },
            Immeuble = new ImmeubleContextDto { Id = r.ImmeubleId, Name = r.ImmeubleName, Location = r.ImmeubleLocation, Type = r.ImmeubleType },
            Floor = r.FloorId is null ? null : new FloorContextDto { Id = r.FloorId.Value, Name = r.FloorName, SequenceNo = r.FloorSeq ?? 0 },
            Unit = new UnitDetailsDto
            {
                Id = r.Id,
                UnitNumber = r.UnitNumber,
                Status = r.Status.ToCode(),
                NumberOfBedrooms = r.NumberOfBedrooms,
                NumberOfBathrooms = r.NumberOfBathrooms,
                ApartmentSurface = r.ApartmentSurface,
                BalconySurface = r.BalconySurface,
                TerraceSurface = r.TerraceSurface,
                GardenSurface = r.GardenSurface,
                TotalSurface = r.TotalSurface,
                SaleableValue = r.SaleableValue,
                SaleableValue1 = r.SaleableValue1,
                View = r.View,
                Orientation = r.Orientation,
                LatestPrice = r.LatestPrice,
                PriceSaleableValue = r.PriceSaleableValue,
                PriceSaleableValue1 = r.PriceSaleableValue1
            }
        });
    }

    /// <summary>Contexts keyed by reservation id.</summary>
    public static async Task<Dictionary<Guid, UnitContextDto>> ForReservationsAsync(ApplicationDbContext db, IEnumerable<Guid> reservationIds, CancellationToken ct)
    {
        var ids = reservationIds.Distinct().ToList();
        if (ids.Count == 0) return new();
        var units = await db.Set<Reservation>().Where(r => ids.Contains(r.Id)).Select(r => new { r.Id, r.UnitId }).ToListAsync(ct);
        var contexts = await ForUnitsAsync(db, units.Select(u => u.UnitId), ct);
        return units.Where(u => contexts.ContainsKey(u.UnitId)).ToDictionary(u => u.Id, u => contexts[u.UnitId]);
    }

    public static T WithLocation<T>(this T dto, UnitContextDto? context) where T : IHasUnitLocation
    {
        if (context is null) return dto;
        dto.ProjectId = context.Project.Id;
        dto.ProjectName = context.Project.Name;
        dto.ImmeubleId = context.Immeuble.Id;
        dto.ImmeubleName = context.Immeuble.Name;
        dto.FloorName = context.Floor?.Name;
        dto.UnitNumber = context.Unit.UnitNumber;
        dto.UnitContext = context;
        return dto;
    }
}
