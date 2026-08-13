using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Floors.CreateFloor;
using ProjectAPI.Api.Application.Immeubles.CreateIImmeubleFeature;
using ProjectAPI.Api.Application.Immeubles.CreateImmeuble;
using ProjectAPI.Api.Application.Immeubles.DeleteImmeuble;
using ProjectAPI.Api.Application.Immeubles.Tracking.AddImmeubleTracking;
using ProjectAPI.Api.Application.Immeubles.UpdateImmeuble;
using ProjectAPI.Api.Application.Units.CreateProjectUnit;
using ProjectAPI.Api.Application.Units.UpdateUnit;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Repositories;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// §6.4 — building/floor/unit creation, update and deletion used to run with
/// zero project-scope checks at all: any PROJECT_ADMIN, regardless of their
/// own ProjectMembership rows, could create, edit, or cascade-delete
/// inventory belonging to any other project. DeleteImmeublesHandler was the
/// highest-blast-radius case (cascades Sales → Reservations → Units →
/// Immeuble); UpdateUnitHandler additionally exposed pricing fields.
/// </summary>
[Collection("sqlserver")]
public class InventoryScopeTests
{
    private readonly SqlServerFixture _fixture;

    public InventoryScopeTests(SqlServerFixture fixture) => _fixture = fixture;

    private void RequireDatabase()
    {
        if (!_fixture.Available)
        {
            throw new InvalidOperationException(
                "These tests assert real project-scope enforcement and cannot run without SQL Server. " +
                $"Connection failed: {_fixture.UnavailableReason}");
        }
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public string? UserId { get; init; }
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();
        public bool IsInRole(string roleCode) => Roles.Contains(roleCode, StringComparer.Ordinal);
        public bool IsGlobalAdmin => IsInRole(RoleCodes.GlobalAdmin);
    }

    private async Task<Guid> SeedProjectAsync(string name)
    {
        await using var db = _fixture.CreateContext();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = name,
            Location = "Casablanca",
            Address = "1 rue de test",
            Description = "seed",
            Module3DLink = string.Empty,
            StatusGlobal = "DRAFT"
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    private async Task<(Guid ImmeubleId, Guid FloorId, Guid UnitId)> SeedInventoryAsync(Guid projectId, string label)
    {
        await using var db = _fixture.CreateContext();

        var immeuble = new Immeuble
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = $"Immeuble {label}",
            Location = "Casablanca",
            Type = "Appartements",
            Description = "seed",
            Images = null,
            MinSellableSurfaceRange = 50,
            MaxSellableSurfaceRange = 100,
            Status = "ComingSoon"
        };

        var floor = new Floor
        {
            Id = Guid.NewGuid(),
            ImmeubleId = immeuble.Id,
            Name = "1",
            SequenceNo = 1
        };

        var unit = new Unit
        {
            Id = Guid.NewGuid(),
            ProjectId = immeuble.Id,
            FloorId = floor.Id,
            UnitNumber = $"A-{label}",
            View = "Jardin",
            Orientation = "Nord",
            Status = UnitCommercialStatus.Available
        };

        db.Immeubles.Add(immeuble);
        db.Set<Floor>().Add(floor);
        db.Units.Add(unit);
        await db.SaveChangesAsync();

        return (immeuble.Id, floor.Id, unit.Id);
    }

    private async Task SeedMembershipAsync(Guid projectId, string userId, string roleCode)
    {
        var (scope, userManager) = _fixture.CreateUserManager();
        using (scope)
        {
            var user = new User
            {
                Id = userId,
                UserName = userId,
                NormalizedUserName = userId.ToUpperInvariant(),
                Email = $"{userId}@test.local",
                NormalizedEmail = $"{userId}@test.local".ToUpperInvariant(),
                FirstName = "Test",
                LastName = "User",
                SecurityStamp = Guid.NewGuid().ToString()
            };

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roleManager.RoleExistsAsync(roleCode))
            {
                await roleManager.CreateAsync(new IdentityRole(roleCode));
            }

            (await userManager.CreateAsync(user)).Succeeded.Should().BeTrue("test user seeding must succeed");
            (await userManager.AddToRoleAsync(user, roleCode)).Succeeded.Should().BeTrue("test role seeding must succeed");
        }

        await using var db = _fixture.CreateContext();
        db.Set<ProjectMembership>().Add(new ProjectMembership
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            UserId = userId,
            RoleCode = roleCode,
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            ValidUntil = null,
            IsActive = true,
            AssignedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    // ------------------------------------------------------------ CreateImmeuble

    [Fact]
    public async Task CreateImmeuble_ProjectAdmin_WithNoMembership_IsDenied()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("Inv-CreateImmeuble-target");

        var admin = new FakeCurrentUser { UserId = "inv-admin-1", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new CreateImmeubleHandler(
            new ImmeubleRepository(db),
            new Infrastructure.Repositories.ProjectRepository(db),
            new ImmeubleTrackingRepository(db),
            scope);

        var command = new CreateImmeubleCommand
        {
            Name = "Rogue building",
            ProjectId = projectId,
            Location = "Casablanca",
            Type = "Appartements",
            Images = string.Empty,
            Description = "test",
        };

        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>(
            "a PROJECT_ADMIN with no membership on this project must not create a building in it");
    }

    [Fact]
    public async Task CreateImmeuble_ProjectAdmin_WithMembership_Succeeds()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("Inv-CreateImmeuble-authorized");
        await SeedMembershipAsync(projectId, "inv-admin-2", RoleCodes.ProjectAdmin);

        var admin = new FakeCurrentUser { UserId = "inv-admin-2", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new CreateImmeubleHandler(
            new ImmeubleRepository(db),
            new Infrastructure.Repositories.ProjectRepository(db),
            new ImmeubleTrackingRepository(db),
            scope);

        var command = new CreateImmeubleCommand
        {
            Name = "Authorized building",
            ProjectId = projectId,
            Location = "Casablanca",
            Type = "Appartements",
            Images = string.Empty,
            Description = "test",
        };

        var id = await handler.Handle(command, CancellationToken.None);
        id.Should().NotBeEmpty();
    }

    // ------------------------------------------------------------ UpdateImmeuble

    [Fact]
    public async Task UpdateImmeuble_ProjectAdmin_WithNoMembership_IsDenied()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("Inv-UpdateImmeuble-target");
        var (immeubleId, _, _) = await SeedInventoryAsync(projectId, "update-target");

        var admin = new FakeCurrentUser { UserId = "inv-admin-3", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new UpdateImmeubleHandler(new ImmeubleRepository(db), new ImmeubleTrackingRepository(db), scope);

        var command = new UpdateImmeubleCommand { Id = immeubleId, Name = "Renamed by intruder" };

        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>(
            "a PROJECT_ADMIN with no membership on this building's project must not edit it");
    }

    // ------------------------------------------------------------ DeleteImmeubles

    [Fact]
    public async Task DeleteImmeuble_ProjectAdmin_WithNoMembership_IsDenied_AndBuildingSurvives()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("Inv-DeleteImmeuble-target");
        var (immeubleId, _, unitId) = await SeedInventoryAsync(projectId, "delete-target");

        var admin = new FakeCurrentUser { UserId = "inv-admin-4", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new DeleteImmeublesHandler(
            new ImmeubleRepository(db),
            new UnitRepository(db),
            new Infrastructure.Repositories.AppointmentRepository(db),
            new Infrastructure.Repositories.ReservationRepository(db),
            new Infrastructure.Repositories.SaleRepository(db),
            scope);

        var act = async () => await handler.Handle(new DeleteImmeublesCommand(immeubleId), CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>(
            "a PROJECT_ADMIN with no membership on this project must not cascade-delete its building/units");

        await using var verify = _fixture.CreateContext();
        (await verify.Immeubles.AnyAsync(i => i.Id == immeubleId)).Should().BeTrue(
            "the building must still exist — the denial must happen before any cascade delete runs");
        (await verify.Units.AnyAsync(u => u.Id == unitId)).Should().BeTrue(
            "units must not be cascade-deleted when the caller is outside the project's perimeter");
    }

    [Fact]
    public async Task DeleteImmeuble_ProjectAdmin_WithMembership_Succeeds()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("Inv-DeleteImmeuble-authorized");
        var (immeubleId, _, unitId) = await SeedInventoryAsync(projectId, "delete-authorized");
        await SeedMembershipAsync(projectId, "inv-admin-5", RoleCodes.ProjectAdmin);

        var admin = new FakeCurrentUser { UserId = "inv-admin-5", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new DeleteImmeublesHandler(
            new ImmeubleRepository(db),
            new UnitRepository(db),
            new Infrastructure.Repositories.AppointmentRepository(db),
            new Infrastructure.Repositories.ReservationRepository(db),
            new Infrastructure.Repositories.SaleRepository(db),
            scope);

        var result = await handler.Handle(new DeleteImmeublesCommand(immeubleId), CancellationToken.None);
        result.Success.Should().BeTrue();

        await using var verify = _fixture.CreateContext();
        (await verify.Immeubles.AnyAsync(i => i.Id == immeubleId)).Should().BeFalse();
        (await verify.Units.AnyAsync(u => u.Id == unitId)).Should().BeFalse();
    }

    // ------------------------------------------------------------ Tracking / Features

    [Fact]
    public async Task AddImmeubleTracking_ProjectAdmin_WithNoMembership_IsDenied()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("Inv-Tracking-target");
        var (immeubleId, _, _) = await SeedInventoryAsync(projectId, "tracking-target");

        var admin = new FakeCurrentUser { UserId = "inv-admin-6", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new AddImmeubleTrackingHandler(new ImmeubleTrackingRepository(db), new ImmeubleRepository(db), scope);

        var act = async () => await handler.Handle(
            new AddImmeubleTrackingCommand { ImmeubleId = immeubleId, StatusUpdate = "Available" },
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>(
            "a PROJECT_ADMIN with no membership on this project must not add tracking entries to its building");
    }

    [Fact]
    public async Task AddImmeubleFeature_ProjectAdmin_WithNoMembership_IsDenied()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("Inv-Feature-target");
        var (immeubleId, _, _) = await SeedInventoryAsync(projectId, "feature-target");

        var admin = new FakeCurrentUser { UserId = "inv-admin-7", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new AddImmeubleFeatureHandler(new ImmeubleFeatureRepository(db), new ImmeubleRepository(db), scope);

        var act = async () => await handler.Handle(
            new AddImmeubleFeatureCommand
            {
                ImmeubleId = immeubleId,
                Features = new List<ImmeubleFeatureRequest> { new() { Name = "Piscine", Icon = "pool" } }
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>(
            "a PROJECT_ADMIN with no membership on this project must not add features to its building");
    }

    // ------------------------------------------------------------ CreateFloor

    [Fact]
    public async Task CreateFloor_ProjectAdmin_WithNoMembership_IsDenied()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("Inv-CreateFloor-target");
        var (immeubleId, _, _) = await SeedInventoryAsync(projectId, "floor-target");

        var admin = new FakeCurrentUser { UserId = "inv-admin-8", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new CreateFloorHandler(db, scope);

        var act = async () => await handler.Handle(
            new CreateFloorCommand { ImmeubleId = immeubleId, Name = "2" },
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>(
            "a PROJECT_ADMIN with no membership on this project must not create a floor in its building");
    }

    // ------------------------------------------------------------ CreateProjectUnit

    [Fact]
    public async Task CreateProjectUnit_ProjectAdmin_WithNoMembership_IsDenied()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("Inv-CreateUnit-target");
        var (immeubleId, floorId, _) = await SeedInventoryAsync(projectId, "unit-target");

        var admin = new FakeCurrentUser { UserId = "inv-admin-9", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new CreateProjectUnitHandler(new UnitRepository(db), new ImmeubleRepository(db), db, scope);

        var command = new CreateProjectUnitCommand
        {
            ProjectId = immeubleId,
            FloorId = floorId,
            UnitNumber = "Z-999",
            View = "Mer",
            Orientation = "Sud",
        };

        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>(
            "a PROJECT_ADMIN with no membership on this project must not create a unit in its building");
    }

    // ------------------------------------------------------------ UpdateUnit

    [Fact]
    public async Task UpdateUnit_ProjectAdmin_WithNoMembership_IsDenied_AndPriceUnchanged()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("Inv-UpdateUnit-target");
        var (_, _, unitId) = await SeedInventoryAsync(projectId, "price-target");

        var admin = new FakeCurrentUser { UserId = "inv-admin-10", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new UpdateUnitHandler(new UnitRepository(db), db, scope);

        var command = new UpdateUnitCommand { Id = unitId, LatestPrice = 1m };

        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>(
            "a PROJECT_ADMIN with no membership on this unit's project must not reprice it");

        await using var verify = _fixture.CreateContext();
        var unit = await verify.Units.SingleAsync(u => u.Id == unitId);
        unit.LatestPrice.Should().BeNull("the denied update must not have applied any field change, including price");
    }

    [Fact]
    public async Task UpdateUnit_ProjectAdmin_WithMembership_Succeeds()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("Inv-UpdateUnit-authorized");
        var (_, _, unitId) = await SeedInventoryAsync(projectId, "price-authorized");
        await SeedMembershipAsync(projectId, "inv-admin-11", RoleCodes.ProjectAdmin);

        var admin = new FakeCurrentUser { UserId = "inv-admin-11", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new UpdateUnitHandler(new UnitRepository(db), db, scope);

        var result = await handler.Handle(new UpdateUnitCommand { Id = unitId, LatestPrice = 999_000m }, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();

        await using var verify = _fixture.CreateContext();
        var unit = await verify.Units.SingleAsync(u => u.Id == unitId);
        unit.LatestPrice.Should().Be(999_000m);
    }

    [Fact]
    public async Task GlobalAdmin_CanCreateEditAndDeleteInventory_InAnyProject()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("Inv-GlobalAdmin-any");
        var (immeubleId, _, unitId) = await SeedInventoryAsync(projectId, "global-admin");

        var globalAdmin = new FakeCurrentUser { UserId = "inv-global-admin", Roles = new[] { RoleCodes.GlobalAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, globalAdmin);

        var updateHandler = new UpdateUnitHandler(new UnitRepository(db), db, scope);
        var updateResult = await updateHandler.Handle(new UpdateUnitCommand { Id = unitId, LatestPrice = 500_000m }, CancellationToken.None);
        updateResult.IsSuccess.Should().BeTrue();

        var deleteHandler = new DeleteImmeublesHandler(
            new ImmeubleRepository(db),
            new UnitRepository(db),
            new Infrastructure.Repositories.AppointmentRepository(db),
            new Infrastructure.Repositories.ReservationRepository(db),
            new Infrastructure.Repositories.SaleRepository(db),
            scope);
        var deleteResult = await deleteHandler.Handle(new DeleteImmeublesCommand(immeubleId), CancellationToken.None);
        deleteResult.Success.Should().BeTrue("a GLOBAL_ADMIN has no perimeter restriction");
    }
}
