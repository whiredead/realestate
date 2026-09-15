using FluentAssertions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Immeubles.GetAllImmeubles;
using ProjectAPI.Api.Application.Immeubles.GetImmeubleById;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Repositories;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// §3/§7 — Immeuble.NumberOfAvailableUnites/NumberOfSoldUnites/NumberOfUnits
/// are legacy denormalized counters nothing in the codebase ever writes to
/// after creation; GetAllImmeublesHandler even copy-pasted NumberOfUnits
/// into NumberOfAvailableUnites, so every immeuble showed 100% available
/// regardless of actual sales. Both GetAllImmeublesHandler and
/// GetImmeubleByIdHandler now compute stock figures live from Unit.Status,
/// the one continuously-maintained source of truth.
/// </summary>
[Collection("sqlserver")]
public class ImmeubleStockCountTests
{
    private readonly SqlServerFixture _fixture;

    public ImmeubleStockCountTests(SqlServerFixture fixture) => _fixture = fixture;

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public string? UserId { get; init; }
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();
        public bool IsInRole(string roleCode) => Roles.Contains(roleCode, StringComparer.Ordinal);
        public bool IsGlobalAdmin => IsInRole(RoleCodes.GlobalAdmin);
    }

    private void RequireDatabase()
    {
        if (!_fixture.Available)
        {
            throw new InvalidOperationException(
                "These tests assert real stock computation and cannot run without SQL Server. " +
                $"Connection failed: {_fixture.UnavailableReason}");
        }
    }

    /// <summary>
    /// Seeds one immeuble with one unit per given status, plus a legacy
    /// NumberOfAvailableUnites/NumberOfSoldUnites value deliberately set
    /// WRONG (mirroring real unmaintained data), to prove the handlers
    /// ignore those stale columns entirely.
    /// </summary>
    private async Task<Guid> SeedImmeubleWithUnitsAsync(string label, params UnitCommercialStatus[] unitStatuses)
    {
        await using var db = _fixture.CreateContext();

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = $"Stock-{label}",
            Location = "Casablanca",
            Address = "1 rue de test",
            Description = "seed",
            Module3DLink = string.Empty,
            StatusGlobal = "SUR_PLAN"
        };

        var immeuble = new Immeuble
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = $"Immeuble {label}",
            Location = "Casablanca",
            Type = "Appartements",
            Description = "seed",
            Images = string.Empty,
            MinSellableSurfaceRange = 50,
            MaxSellableSurfaceRange = 100,
            Status = "ComingSoon",
            // Deliberately wrong stale legacy counters — a correct fix must
            // never read these.
            NumberOfUnits = 999,
            NumberOfAvailableUnites = 999,
            NumberOfSoldUnites = 0,
        };

        var floor = new Floor { Id = Guid.NewGuid(), ImmeubleId = immeuble.Id, Name = "1", SequenceNo = 1 };

        db.Projects.Add(project);
        db.Immeubles.Add(immeuble);
        db.Set<Floor>().Add(floor);

        var i = 0;
        foreach (var status in unitStatuses)
        {
            db.Units.Add(new Unit
            {
                Id = Guid.NewGuid(),
                ProjectId = immeuble.Id,
                FloorId = floor.Id,
                UnitNumber = $"U-{label}-{i++}",
                View = "Jardin",
                Orientation = "Nord",
                Status = status
            });
        }

        await db.SaveChangesAsync();
        return immeuble.Id;
    }

    [Fact]
    public async Task GetImmeubleById_ComputesAvailableAndSoldCounts_FromLiveUnitStatus_NotStaleColumns()
    {
        RequireDatabase();
        var immeubleId = await SeedImmeubleWithUnitsAsync(
            "byid",
            UnitCommercialStatus.Available,
            UnitCommercialStatus.Available,
            UnitCommercialStatus.HoldPendingApproval,
            UnitCommercialStatus.Reserved,
            UnitCommercialStatus.Contracted,
            UnitCommercialStatus.Sold,
            UnitCommercialStatus.Delivered,
            UnitCommercialStatus.Suspended,
            UnitCommercialStatus.Cancelled);

        var db = _fixture.CreateContext();
        var handler = new GetImmeubleByIdHandler(new ImmeubleRepository(db));

        var result = await handler.Handle(new GetImmeubleByIdQuery(immeubleId), CancellationToken.None);

        result.NumberOfUnits.Should().Be(9, "total must count every seeded unit, not the stale 999 column");
        result.NumberOfAvailableUnites.Should().Be(2,
            "only AVAILABLE units are available — HOLD_PENDING_APPROVAL/RESERVED/CONTRACTED/SOLD/DELIVERED/SUSPENDED/CANCELLED must not count, " +
            "and the stale 999 column must never be read");
        result.NumberOfSoldUnites.Should().Be(2, "SOLD and DELIVERED both count as sold; the other five statuses must not");
    }

    [Fact]
    public async Task GetAllImmeubles_ComputesAvailableAndSoldCounts_FromLiveUnitStatus_NotStaleColumns()
    {
        RequireDatabase();
        var immeubleId = await SeedImmeubleWithUnitsAsync(
            "list",
            UnitCommercialStatus.Available,
            UnitCommercialStatus.Sold,
            UnitCommercialStatus.Sold,
            UnitCommercialStatus.Reserved);

        var db = _fixture.CreateContext();
        var globalAdmin = new FakeCurrentUser { UserId = "stock-count-global-admin", Roles = new[] { RoleCodes.GlobalAdmin } };
        var handler = new GetAllImmeublesHandler(new ImmeubleRepository(db), globalAdmin, new ProjectScopeService(db, globalAdmin));

        var result = await handler.Handle(
            new GetAllImmeublesQuery { PageSize = 100 },
            CancellationToken.None);

        var row = result.Data.Should().ContainSingle(i => i.Id == immeubleId).Subject;
        row.NumberOfUnits.Should().Be(4);
        row.NumberOfAvailableUnites.Should().Be(1,
            "the list handler previously copy-pasted NumberOfUnits into NumberOfAvailableUnites, " +
            "showing every unit as available regardless of sales — this must now reflect only the true AVAILABLE unit");
        row.NumberOfSoldUnites.Should().Be(2);
    }

    [Fact]
    public async Task GetImmeubleById_AllUnitsSold_ReportsZeroAvailable()
    {
        RequireDatabase();
        var immeubleId = await SeedImmeubleWithUnitsAsync(
            "sold-out",
            UnitCommercialStatus.Sold,
            UnitCommercialStatus.Delivered);

        var db = _fixture.CreateContext();
        var handler = new GetImmeubleByIdHandler(new ImmeubleRepository(db));

        var result = await handler.Handle(new GetImmeubleByIdQuery(immeubleId), CancellationToken.None);

        result.NumberOfAvailableUnites.Should().Be(0,
            "a fully sold building must report zero available units, not the stale total-units count");
        result.NumberOfSoldUnites.Should().Be(2);
        result.SellsPercentage.Should().Be(100);
    }

    [Fact]
    public async Task GetImmeubleById_NoUnits_ReportsZeroesWithoutDividingByZero()
    {
        RequireDatabase();
        var immeubleId = await SeedImmeubleWithUnitsAsync("empty");

        var db = _fixture.CreateContext();
        var handler = new GetImmeubleByIdHandler(new ImmeubleRepository(db));

        var result = await handler.Handle(new GetImmeubleByIdQuery(immeubleId), CancellationToken.None);

        result.NumberOfUnits.Should().Be(0);
        result.NumberOfAvailableUnites.Should().Be(0);
        result.NumberOfSoldUnites.Should().Be(0);
        result.SellsPercentage.Should().Be(0);
    }
}
