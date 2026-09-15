using ProjectAPI.Domain.Identity.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.ProjectAgentAssignmentConfig.GetProjectAgentAssignmentConfig;
using ProjectAPI.Api.Application.ProjectAgentAssignmentConfig.SetProjectAgentAssignmentConfig;
using ProjectAPI.Api.Application.Purchases.GetUserPurchases;
using ProjectAPI.Api.Application.Sales.GetSalesByUser;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Purchases.Entities;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Repositories;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// §6.4 — similarly-shaped gaps from the final Batch 1 completeness sweep
/// (the legacy delivery scheduling flow they also covered has been removed —
/// Handovers is the only delivery flow): the agent-assignment-config (lead-routing rule) read/write had no
/// project scope; and GetSalesByUser/GetUserPurchases had no buyer-ownership
/// check, letting any authenticated caller read another buyer's financial
/// history by UserId alone.
/// </summary>
[Collection("sqlserver")]
public class DeliveryAssignmentConfigAndBuyerOwnershipScopeTests
{
    private readonly SqlServerFixture _fixture;

    public DeliveryAssignmentConfigAndBuyerOwnershipScopeTests(SqlServerFixture fixture) => _fixture = fixture;

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
            StatusGlobal = "SUR_PLAN"
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    private async Task<(Guid UnitId, Guid SaleId)> SeedUnitAndSaleAsync(Guid projectId, string label)
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

        var floor = new Floor { Id = Guid.NewGuid(), ImmeubleId = immeuble.Id, Name = "1", SequenceNo = 1 };

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

        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            UnitId = unit.Id,
            BuyerFirstName = "Test",
            BuyerLastName = "Buyer",
            BuyerEmail = $"buyer-{label}@test.local",
            BuyerPhoneNumber = "0600000000",
            SaleDate = DateTime.UtcNow,
            TotalPrice = 1_000_000m,
        };

        db.Immeubles.Add(immeuble);
        db.Set<Floor>().Add(floor);
        db.Units.Add(unit);
        db.Set<Sale>().Add(sale);
        await db.SaveChangesAsync();

        return (unit.Id, sale.Id);
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

            var roleManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<ProjectAPI.Domain.Identity.Entities.Role>>();
            if (!await roleManager.RoleExistsAsync(roleCode))
            {
                await roleManager.CreateAsync(new ProjectAPI.Domain.Identity.Entities.Role { Id = Guid.NewGuid().ToString(), Name = roleCode, DisplayName = roleCode });
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

    // ------------------------------------------------------------ ProjectAgentAssignmentConfig

    [Fact]
    public async Task SetAssignmentConfig_ProjectAdmin_WithNoMembership_IsDenied()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("AssignConfig-target");

        var admin = new FakeCurrentUser { UserId = "ac-admin-1", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new SetProjectAgentAssignmentConfigHandler(
            new ProjectAgentAssignmentConfigRepository(db),
            new ProjectMembershipRepository(db),
            scope);

        var command = new SetProjectAgentAssignmentConfigCommand { ProjectId = projectId, RuleType = "ROUND_ROBIN" };

        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>(
            "a PROJECT_ADMIN with no membership on this project must not rewrite its lead-routing rule");
    }

    [Fact]
    public async Task GetAssignmentConfig_ProjectAdmin_WithNoMembership_IsDenied()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("GetAssignConfig-target");

        var admin = new FakeCurrentUser { UserId = "ac-admin-2", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new GetProjectAgentAssignmentConfigHandler(new ProjectAgentAssignmentConfigRepository(db), scope);

        var act = async () => await handler.Handle(
            new GetProjectAgentAssignmentConfigQuery { ProjectId = projectId },
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>(
            "a PROJECT_ADMIN with no membership on this project must not read its lead-routing rule");
    }

    // ------------------------------------------------------------ Buyer ownership: Sales / Purchases

    [Fact]
    public async Task GetSalesByUser_Buyer_CannotReadAnotherBuyersSales()
    {
        RequireDatabase();
        var buyer = new FakeCurrentUser { UserId = "buyer-intruder-1", Roles = new[] { RoleCodes.Buyer } };
        var db = _fixture.CreateContext();
        var handler = new ProjectAPI.Api.Application.Sales.GetSalesByUser.GetSalesByUserHandler(
            new SaleRepository(db), new PurchaseRepository(db), buyer);

        var act = async () => await handler.Handle(
            new GetSalesByUserQuery { UserId = "buyer-victim-1" },
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>(
            "a buyer must never read another buyer's sales history by supplying an arbitrary UserId");
    }

    [Fact]
    public async Task GetSalesByUser_Buyer_CanReadOwnSales()
    {
        RequireDatabase();
        var buyer = new FakeCurrentUser { UserId = "buyer-self-1", Roles = new[] { RoleCodes.Buyer } };
        var db = _fixture.CreateContext();
        var handler = new ProjectAPI.Api.Application.Sales.GetSalesByUser.GetSalesByUserHandler(
            new SaleRepository(db), new PurchaseRepository(db), buyer);

        var result = await handler.Handle(new GetSalesByUserQuery { UserId = "buyer-self-1" }, CancellationToken.None);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSalesByUser_InternalRole_CanReadAnyBuyersSales()
    {
        RequireDatabase();
        var agent = new FakeCurrentUser { UserId = "sales-agent-1", Roles = new[] { RoleCodes.SalesAgent } };
        var db = _fixture.CreateContext();
        var handler = new ProjectAPI.Api.Application.Sales.GetSalesByUser.GetSalesByUserHandler(
            new SaleRepository(db), new PurchaseRepository(db), agent);

        var result = await handler.Handle(new GetSalesByUserQuery { UserId = "any-buyer-id" }, CancellationToken.None);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetUserPurchases_Buyer_CannotReadAnotherBuyersPurchases()
    {
        RequireDatabase();
        var buyer = new FakeCurrentUser { UserId = "buyer-intruder-2", Roles = new[] { RoleCodes.Buyer } };
        var db = _fixture.CreateContext();
        var handler = new GetUserPurchasesHandler(
            new PurchaseRepository(db), new ReservationRepository(db), new SaleRepository(db), buyer);

        var act = async () => await handler.Handle(
            new GetUserPurchasesQuery { UserId = "buyer-victim-2" },
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>(
            "a buyer must never read another buyer's purchase history by supplying an arbitrary UserId");
    }

    [Fact]
    public async Task GetUserPurchases_Buyer_CanReadOwnPurchases()
    {
        RequireDatabase();
        var buyer = new FakeCurrentUser { UserId = "buyer-self-2", Roles = new[] { RoleCodes.Buyer } };
        var db = _fixture.CreateContext();
        var handler = new GetUserPurchasesHandler(
            new PurchaseRepository(db), new ReservationRepository(db), new SaleRepository(db), buyer);

        var result = await handler.Handle(new GetUserPurchasesQuery { UserId = "buyer-self-2" }, CancellationToken.None);

        result.Should().NotBeNull();
    }
}
