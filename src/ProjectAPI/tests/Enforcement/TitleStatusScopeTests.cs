using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Construction.GetTitleStatus;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// §16 — GetTitleStatusHandler used to run with no project scope or buyer
/// ownership check at all: any authenticated caller, any role, any project,
/// could read any unit's title status and full history (including internal
/// Reason/DocumentUrl fields) by unit id alone. This is reachable from both
/// an internal unit page and a buyer's own property file (see
/// FRONTEND_BACKEND_INTEGRATION.md), so both perimeters must hold.
/// </summary>
[Collection("sqlserver")]
public class TitleStatusScopeTests
{
    private readonly SqlServerFixture _fixture;

    public TitleStatusScopeTests(SqlServerFixture fixture) => _fixture = fixture;

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

    private async Task<(Guid ProjectId, Guid UnitId)> SeedUnitAsync(string label)
    {
        await using var db = _fixture.CreateContext();

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = $"Project {label}",
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
            Status = UnitCommercialStatus.Sold
        };

        db.Projects.Add(project);
        db.Immeubles.Add(immeuble);
        db.Set<Floor>().Add(floor);
        db.Units.Add(unit);
        await db.SaveChangesAsync();

        return (project.Id, unit.Id);
    }

    /// <summary>
    /// Seeds a real AspNetUsers row (+ role, via UserManager) — ProjectMemberships.UserId
    /// carries an FK to AspNetUsers, so a bare string id fails the insert.
    /// </summary>
    private async Task SeedUserAsync(string userId, string roleCode)
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
    }

    private async Task SeedMembershipAsync(Guid projectId, string userId, string roleCode)
    {
        await SeedUserAsync(userId, roleCode);

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

    private async Task SeedReservationAsync(Guid unitId, string buyerId)
    {
        await using var db = _fixture.CreateContext();
        db.Set<Reservation>().Add(new Reservation
        {
            Id = Guid.NewGuid(),
            UnitId = unitId,
            BuyerId = buyerId,
            TotalPropertyPrice = 1_000_000m,
            ReservationAmount = 100_000m,
            ReservationDate = DateTime.UtcNow,
            Status = ReservationStatus.Sold
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ProjectAdmin_WithNoMembership_CannotReadTitleStatus()
    {
        RequireDatabase();
        var (_, unitId) = await SeedUnitAsync("no-membership");

        var admin = new FakeCurrentUser { UserId = "title-admin-1", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new GetTitleStatusHandler(db, scope);

        var act = async () => await handler.Handle(new GetTitleStatusQuery { UnitId = unitId }, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>(
            "a PROJECT_ADMIN with no membership on this unit's project must not read its title status/history");
    }

    [Fact]
    public async Task ProjectAdmin_WithMembership_OnDifferentProject_CannotReadTitleStatus()
    {
        RequireDatabase();
        var (_, unitId) = await SeedUnitAsync("wrong-project");
        var (otherProjectId, _) = await SeedUnitAsync("other-project");
        await SeedMembershipAsync(otherProjectId, "title-admin-2", RoleCodes.ProjectAdmin);

        var admin = new FakeCurrentUser { UserId = "title-admin-2", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new GetTitleStatusHandler(db, scope);

        var act = async () => await handler.Handle(new GetTitleStatusQuery { UnitId = unitId }, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>(
            "membership on a different project must not grant access to this unit's title status");
    }

    [Fact]
    public async Task ProjectAdmin_WithMembership_OnSameProject_CanReadTitleStatus()
    {
        RequireDatabase();
        var (projectId, unitId) = await SeedUnitAsync("correct-project");
        await SeedMembershipAsync(projectId, "title-admin-3", RoleCodes.ProjectAdmin);

        var admin = new FakeCurrentUser { UserId = "title-admin-3", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new GetTitleStatusHandler(db, scope);

        var result = await handler.Handle(new GetTitleStatusQuery { UnitId = unitId }, CancellationToken.None);

        result.UnitId.Should().Be(unitId);
    }

    [Fact]
    public async Task GlobalAdmin_CanReadAnyUnitsTitleStatus()
    {
        RequireDatabase();
        var (_, unitId) = await SeedUnitAsync("global-admin");

        var globalAdmin = new FakeCurrentUser { UserId = "title-global-admin", Roles = new[] { RoleCodes.GlobalAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, globalAdmin);
        var handler = new GetTitleStatusHandler(db, scope);

        var result = await handler.Handle(new GetTitleStatusQuery { UnitId = unitId }, CancellationToken.None);

        result.UnitId.Should().Be(unitId);
    }

    [Fact]
    public async Task Buyer_WithNoReservationOnUnit_CannotReadTitleStatus()
    {
        RequireDatabase();
        var (_, unitId) = await SeedUnitAsync("buyer-no-reservation");

        var buyer = new FakeCurrentUser { UserId = "title-buyer-1", Roles = new[] { RoleCodes.Buyer } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, buyer);
        var handler = new GetTitleStatusHandler(db, scope);

        var act = async () => await handler.Handle(new GetTitleStatusQuery { UnitId = unitId }, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>(
            "a buyer who holds no reservation on this unit must not read its title status/history");
    }

    [Fact]
    public async Task Buyer_WithOwnReservationOnUnit_CanReadTitleStatus()
    {
        RequireDatabase();
        var (_, unitId) = await SeedUnitAsync("buyer-owns");
        await SeedReservationAsync(unitId, "title-buyer-2");

        var buyer = new FakeCurrentUser { UserId = "title-buyer-2", Roles = new[] { RoleCodes.Buyer } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, buyer);
        var handler = new GetTitleStatusHandler(db, scope);

        var result = await handler.Handle(new GetTitleStatusQuery { UnitId = unitId }, CancellationToken.None);

        result.UnitId.Should().Be(unitId);
    }

    [Fact]
    public async Task Buyer_CannotReadTitleStatus_ForAnotherBuyersUnit()
    {
        RequireDatabase();
        var (_, unitId) = await SeedUnitAsync("buyer-other-owns");
        await SeedReservationAsync(unitId, "title-buyer-owner");

        var otherBuyer = new FakeCurrentUser { UserId = "title-buyer-intruder", Roles = new[] { RoleCodes.Buyer } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, otherBuyer);
        var handler = new GetTitleStatusHandler(db, scope);

        var act = async () => await handler.Handle(new GetTitleStatusQuery { UnitId = unitId }, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>(
            "a buyer must never read another buyer's unit title status, even authenticated and even as BUYER role");
    }
}
