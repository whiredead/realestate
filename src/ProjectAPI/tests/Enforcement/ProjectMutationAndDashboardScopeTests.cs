using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectAPI.Api.Application.AdminDashboards.Dashboard;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Projects.AddProjectFratures;
using ProjectAPI.Api.Application.Projects.RemoveProject;
using ProjectAPI.Api.Application.Projects.RemoveProjectFeatures;
using ProjectAPI.Api.Application.Projects.UpdateProjects;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Repositories;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// §6.4 — RemoveProjectHandler (unscoped cascading hard-delete of a whole
/// project), UpdateProjectHandler, AddProjectFeatureHandler,
/// RemoveProjectFeatureHandler and GetAdminDashboardHandler all ran with no
/// project-scope check: a PROJECT_ADMIN could edit, destroy, or read
/// aggregate figures for a project outside their own perimeter.
/// </summary>
[Collection("sqlserver")]
public class ProjectMutationAndDashboardScopeTests
{
    private readonly SqlServerFixture _fixture;

    public ProjectMutationAndDashboardScopeTests(SqlServerFixture fixture) => _fixture = fixture;

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

    private async Task SeedMembershipAsync(Guid projectId, string userId, string roleCode)
    {
        var (scope, userManager) = _fixture.CreateUserManager();
        using (scope)
        {
            var user = new Domain.Users.Entities.User
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

            var roleManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole>>();
            if (!await roleManager.RoleExistsAsync(roleCode))
            {
                await roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole(roleCode));
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

    // ------------------------------------------------------------ RemoveProject

    [Fact]
    public async Task RemoveProject_ProjectAdmin_WithNoMembership_IsDenied_AndProjectSurvives()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("RemoveProject-target");

        var admin = new FakeCurrentUser { UserId = "rp-admin-1", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new RemoveProjectHandler(db, scope);

        var act = async () => await handler.Handle(new RemoveProjectCommand { ProjectId = projectId }, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>(
            "a PROJECT_ADMIN with no membership on this project must not hard-delete it");

        await using var verify = _fixture.CreateContext();
        (await verify.Projects.AnyAsync(p => p.Id == projectId)).Should().BeTrue(
            "the project must survive a denied cross-project delete attempt");
    }

    [Fact]
    public async Task RemoveProject_ProjectAdmin_WithMembership_Succeeds()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("RemoveProject-authorized");
        await SeedMembershipAsync(projectId, "rp-admin-2", RoleCodes.ProjectAdmin);

        var admin = new FakeCurrentUser { UserId = "rp-admin-2", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new RemoveProjectHandler(db, scope);

        var result = await handler.Handle(new RemoveProjectCommand { ProjectId = projectId }, CancellationToken.None);
        result.Success.Should().BeTrue();

        await using var verify = _fixture.CreateContext();
        (await verify.Projects.AnyAsync(p => p.Id == projectId)).Should().BeFalse();
    }

    // ------------------------------------------------------------ UpdateProject

    [Fact]
    public async Task UpdateProject_ProjectAdmin_WithNoMembership_IsDenied()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("UpdateProject-target");

        var admin = new FakeCurrentUser { UserId = "up-admin-1", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new UpdateProjectHandler(
            new Infrastructure.Repositories.ProjectRepository(db),
            new QuartierRepository(db),
            scope,
            // §7.2 media validation. This test sends no media, so the policy is
            // never consulted; it is opened up anyway so a future edit here
            // fails on the perimeter rule under test, not on a host rule.
            MediaPolicyStub.Permissive(),
            db);

        var act = async () => await handler.Handle(
            new UpdateProjectCommand { Id = projectId, Name = "Renamed by intruder", Location = "Rabat" },
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>(
            "a PROJECT_ADMIN with no membership on this project must not edit it");
    }

    // ------------------------------------------------------------ AddProjectFeature / RemoveProjectFeature

    [Fact]
    public async Task AddProjectFeature_ProjectAdmin_WithNoMembership_IsDenied()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("AddFeature-target");

        var admin = new FakeCurrentUser { UserId = "pf-admin-1", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new AddProjectFeatureHandler(new ProjectFeatureRepository(db), scope);

        var command = new AddProjectFeatureCommand
        {
            ProjectId = projectId,
            Features = new List<ProjectFeatureRequest> { new() { Name = "Piscine", Icon = "pool" } }
        };

        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>(
            "a PROJECT_ADMIN with no membership on this project must not add a feature to it");
    }

    [Fact]
    public async Task RemoveProjectFeature_ProjectAdmin_WithNoMembership_IsDenied()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("RemoveFeature-target");
        Guid featureId;
        await using (var db = _fixture.CreateContext())
        {
            var feature = new ProjectFeature { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Piscine", Icon = "pool" };
            db.Set<ProjectFeature>().Add(feature);
            await db.SaveChangesAsync();
            featureId = feature.Id;
        }

        var admin = new FakeCurrentUser { UserId = "pf-admin-2", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db2 = _fixture.CreateContext();
        var scope = new ProjectScopeService(db2, admin);
        var handler = new RemoveProjectFeatureHandler(new ProjectFeatureRepository(db2), scope);

        var act = async () => await handler.Handle(
            new RemoveProjectFeatureCommand { ProjectId = projectId, FeatureIds = new List<Guid> { featureId } },
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>(
            "a PROJECT_ADMIN with no membership on this project must not remove its features");

        await using var verify = _fixture.CreateContext();
        (await verify.Set<ProjectFeature>().AnyAsync(f => f.Id == featureId)).Should().BeTrue(
            "the feature must survive a denied cross-project delete attempt");
    }

    // ------------------------------------------------------------ GetAdminDashboard

    [Fact]
    public async Task GetAdminDashboard_ProjectAdmin_ProjectCount_ExcludesOtherProjects()
    {
        RequireDatabase();
        var ownProjectId = await SeedProjectAsync("Dashboard-own");
        await SeedProjectAsync("Dashboard-other");
        await SeedMembershipAsync(ownProjectId, "dash-admin-1", RoleCodes.ProjectAdmin);

        var admin = new FakeCurrentUser { UserId = "dash-admin-1", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new GetAdminDashboardHandler(db, scope);

        var result = await handler.Handle(new AdminDashboardQuery(), CancellationToken.None);

        result.TotalProjects.Should().Be(1,
            "a PROJECT_ADMIN's dashboard must count only their own scoped projects, not every project platform-wide");
    }

    [Fact]
    public async Task GetAdminDashboard_GlobalAdmin_SeesAllProjects()
    {
        RequireDatabase();
        await SeedProjectAsync("Dashboard-global-a");
        await SeedProjectAsync("Dashboard-global-b");

        var globalAdmin = new FakeCurrentUser { UserId = "dash-global-admin", Roles = new[] { RoleCodes.GlobalAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, globalAdmin);
        var handler = new GetAdminDashboardHandler(db, scope);

        var result = await handler.Handle(new AdminDashboardQuery(), CancellationToken.None);

        result.TotalProjects.Should().BeGreaterThanOrEqualTo(2,
            "a GLOBAL_ADMIN's dashboard must remain unrestricted across all projects");
    }
}
