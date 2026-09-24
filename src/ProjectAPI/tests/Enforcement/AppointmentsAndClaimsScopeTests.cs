using ProjectAPI.Domain.Identity.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectAPI.Api.Application.Appointments.GetAppointmentById;
using ProjectAPI.Api.Application.Appointments.GetAppointments;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Sales.AfterSales.GetClaims;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Repositories;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// §6.4 — GetAppointmentsHandler carried no project scope at all (a
/// SALES_AGENT with no filters saw every appointment across every project,
/// including colleagues'), GetAppointmentByIdHandler enforced SALES_AGENT
/// ownership but not PROJECT_ADMIN perimeter, and GetClaimsHandler filtered
/// only by caller-supplied query params with no restriction derived from the
/// caller's own role/membership at all (a TECHNICIAN with no filters saw
/// every claim in every project).
/// </summary>
[Collection("sqlserver")]
public class AppointmentsAndClaimsScopeTests
{
    private readonly SqlServerFixture _fixture;

    public AppointmentsAndClaimsScopeTests(SqlServerFixture fixture) => _fixture = fixture;

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

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
            if (!await roleManager.RoleExistsAsync(roleCode))
            {
                await roleManager.CreateAsync(new Role { Id = Guid.NewGuid().ToString(), Name = roleCode, DisplayName = roleCode });
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

    private async Task<Guid> SeedAppointmentAsync(Guid projectId, string? salesAgentId, string label)
    {
        await using var db = _fixture.CreateContext();
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            SalesAgentId = salesAgentId,
            AppointmentDate = DateTime.UtcNow.AddDays(2),
            Status = "REQUESTED",
            Name = $"Guest-{label}",
            LastName = "Test",
            Email = $"guest-{label}@test.local",
            PhoneNumber = "0600000000",
        };
        db.Set<Appointment>().Add(appointment);
        await db.SaveChangesAsync();
        return appointment.Id;
    }

    private async Task<Guid> SeedClaimAsync(Guid unitId, string? assignedAgentId, string label)
    {
        await using var db = _fixture.CreateContext();
        var claim = new AfterSaleClaim
        {
            Id = Guid.NewGuid(),
            UnitId = unitId,
            Title = $"Claim {label}",
            Description = "seed",
            Category = ClaimCategory.General,
            Priority = ClaimPriority.Normal,
            Status = ClaimStatus.Submitted,
            AssignedAgentId = assignedAgentId,
            CreatedAt = DateTime.UtcNow
        };
        db.Set<AfterSaleClaim>().Add(claim);
        await db.SaveChangesAsync();
        return claim.Id;
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

        var immeuble = new Domain.Immeubles.Entities.Immeuble
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

        var floor = new Domain.Immeubles.Entities.Floor
        {
            Id = Guid.NewGuid(),
            ImmeubleId = immeuble.Id,
            Name = "1",
            SequenceNo = 1
        };

        var unit = new Domain.Immeubles.Entities.Unit
        {
            Id = Guid.NewGuid(),
            ProjectId = immeuble.Id,
            FloorId = floor.Id,
            UnitNumber = $"A-{label}",
            View = "Jardin",
            Orientation = "Nord",
            Status = Domain.Immeubles.Entities.UnitCommercialStatus.Delivered
        };

        db.Projects.Add(project);
        db.Immeubles.Add(immeuble);
        db.Set<Domain.Immeubles.Entities.Floor>().Add(floor);
        db.Units.Add(unit);
        await db.SaveChangesAsync();

        return (project.Id, unit.Id);
    }

    // ------------------------------------------------------------ GetAppointments (list)

    [Fact]
    public async Task GetAppointments_ProjectAdmin_NeverSeesAppointmentsOutsideOwnPerimeter()
    {
        RequireDatabase();
        var ownProject = await SeedProjectAsync("Appt-List-own");
        var otherProject = await SeedProjectAsync("Appt-List-other");
        await SeedMembershipAsync(ownProject, "appt-admin-1", RoleCodes.ProjectAdmin);
        await SeedAppointmentAsync(ownProject, null, "own");
        var otherApptId = await SeedAppointmentAsync(otherProject, null, "other");

        var admin = new FakeCurrentUser { UserId = "appt-admin-1", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new GetAppointmentsHandler(new AppointmentRepository(db), scope, admin);

        var result = await handler.Handle(new GetAppointmentsQuery { PageSize = 100 }, CancellationToken.None);

        result.Data.Should().NotContain(a => a.Id == otherApptId,
            "a PROJECT_ADMIN with no membership on the other project must never see its appointments");
    }

    [Fact]
    public async Task GetAppointments_SalesAgent_SeesEveryAppointmentOfAssignedProjects_ButNoOtherProject()
    {
        RequireDatabase();
        var shared = await SeedProjectAsync("Appt-List-shared");
        var elsewhere = await SeedProjectAsync("Appt-List-elsewhere");
        await SeedMembershipAsync(shared, "appt-agent-own", RoleCodes.SalesAgent);
        await SeedMembershipAsync(shared, "appt-agent-colleague", RoleCodes.SalesAgent);
        var ownApptId = await SeedAppointmentAsync(shared, "appt-agent-own", "mine");
        var colleagueApptId = await SeedAppointmentAsync(shared, "appt-agent-colleague", "colleague");
        var otherProjectApptId = await SeedAppointmentAsync(elsewhere, "appt-agent-colleague", "other-project");

        var agent = new FakeCurrentUser { UserId = "appt-agent-own", Roles = new[] { RoleCodes.SalesAgent } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, agent);
        var handler = new GetAppointmentsHandler(new AppointmentRepository(db), scope, agent);

        var result = await handler.Handle(new GetAppointmentsQuery { PageSize = 100 }, CancellationToken.None);

        result.Data.Should().Contain(a => a.Id == ownApptId);
        result.Data.Should().Contain(a => a.Id == colleagueApptId,
            "a SALES_AGENT sees all the appointments of the projects assigned to them, including a colleague's");
        result.Data.Should().NotContain(a => a.Id == otherProjectApptId,
            "but never an appointment of a project they are not assigned to");
    }

    [Fact]
    public async Task GetAppointments_ProjectAdmin_CannotBypassScopeViaProjectIdFilter()
    {
        RequireDatabase();
        var ownProject = await SeedProjectAsync("Appt-List-bypass-own");
        var otherProject = await SeedProjectAsync("Appt-List-bypass-other");
        await SeedMembershipAsync(ownProject, "appt-admin-2", RoleCodes.ProjectAdmin);
        var otherApptId = await SeedAppointmentAsync(otherProject, null, "bypass-target");

        var admin = new FakeCurrentUser { UserId = "appt-admin-2", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new GetAppointmentsHandler(new AppointmentRepository(db), scope, admin);

        // Explicitly requesting the other project's id must not widen scope.
        var result = await handler.Handle(
            new GetAppointmentsQuery { ProjectId = otherProject, PageSize = 100 },
            CancellationToken.None);

        result.Data.Should().NotContain(a => a.Id == otherApptId,
            "an explicit ProjectId query filter must not bypass the caller's own scope");
    }

    // ------------------------------------------------------------ GetAppointmentById

    [Fact]
    public async Task GetAppointmentById_ProjectAdmin_WithNoMembership_IsDenied()
    {
        RequireDatabase();
        var project = await SeedProjectAsync("Appt-ById-target");
        var apptId = await SeedAppointmentAsync(project, null, "target");

        var admin = new FakeCurrentUser { UserId = "appt-admin-3", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new GetAppointmentByIdHandler(new AppointmentRepository(db), admin, scope);

        var act = async () => await handler.Handle(new GetAppointmentByIdQuery { AppointmentId = apptId }, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>(
            "a PROJECT_ADMIN with no membership on this appointment's project must not read it");
    }

    // ------------------------------------------------------------ GetClaims (list)

    [Fact]
    public async Task GetClaims_Technician_OnlySeesClaimsAssignedToThem()
    {
        RequireDatabase();
        var (projectId, unitId) = await SeedUnitAsync("claims-tech");
        await SeedMembershipAsync(projectId, "claims-tech-own", RoleCodes.Technician);
        await SeedClaimAsync(unitId, "claims-tech-own", "assigned-to-me");
        var othersClaimId = await SeedClaimAsync(unitId, "claims-tech-other", "assigned-to-colleague");

        var tech = new FakeCurrentUser { UserId = "claims-tech-own", Roles = new[] { RoleCodes.Technician } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, tech);
        var handler = new GetClaimsHandler(new AfterSaleClaimRepository(db), scope, tech, db);

        var result = await handler.Handle(new GetClaimsQuery { PageSize = 100 }, CancellationToken.None);

        result.Data.Should().NotContain(c => c.Id == othersClaimId,
            "a TECHNICIAN must only see claims assigned to them, regardless of any AgentId filter (or lack of one)");
    }

    [Fact]
    public async Task GetClaims_ProjectAdmin_NeverSeesClaimsOutsideOwnPerimeter()
    {
        RequireDatabase();
        var (ownProjectId, ownUnitId) = await SeedUnitAsync("claims-admin-own");
        var (_, otherUnitId) = await SeedUnitAsync("claims-admin-other");
        await SeedMembershipAsync(ownProjectId, "claims-admin-1", RoleCodes.ProjectAdmin);
        await SeedClaimAsync(ownUnitId, null, "own-project-claim");
        var otherClaimId = await SeedClaimAsync(otherUnitId, null, "other-project-claim");

        var admin = new FakeCurrentUser { UserId = "claims-admin-1", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new GetClaimsHandler(new AfterSaleClaimRepository(db), scope, admin, db);

        var result = await handler.Handle(new GetClaimsQuery { PageSize = 100 }, CancellationToken.None);

        result.Data.Should().NotContain(c => c.Id == otherClaimId,
            "a PROJECT_ADMIN with no membership on the other project must never see its claims");
    }

    [Fact]
    public async Task GetClaims_GlobalAdmin_SeesClaimsAcrossAllProjects()
    {
        RequireDatabase();
        var (_, unitA) = await SeedUnitAsync("claims-global-a");
        var (_, unitB) = await SeedUnitAsync("claims-global-b");
        var claimA = await SeedClaimAsync(unitA, null, "global-a");
        var claimB = await SeedClaimAsync(unitB, null, "global-b");

        var globalAdmin = new FakeCurrentUser { UserId = "claims-global-admin", Roles = new[] { RoleCodes.GlobalAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, globalAdmin);
        var handler = new GetClaimsHandler(new AfterSaleClaimRepository(db), scope, globalAdmin, db);

        var result = await handler.Handle(new GetClaimsQuery { PageSize = 100 }, CancellationToken.None);

        result.Data.Should().Contain(c => c.Id == claimA);
        result.Data.Should().Contain(c => c.Id == claimB);
    }
}
