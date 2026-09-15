using ProjectAPI.Domain.Identity.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Immeubles.CreateImmeuble;
using ProjectAPI.Api.Application.Immeubles.GetAllImmeubles;
using ProjectAPI.Api.Application.Projects.GetAllProjects;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Repositories;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// Acceptance rerun regression F7: GetAllProjectsHandler and
/// GetAllImmeublesHandler are [AllowAnonymous] to serve the public catalogue,
/// but the admin console's project/building lists (ProjectsListPage.tsx,
/// ImmeublesListPage.tsx) hit these exact same endpoints. Every mutation in
/// this codebase was scoped to ProjectMembership except these two list
/// reads, so a membership-less PROJECT_ADMIN could still list and open every
/// project/building even though every write on them was already denied.
/// </summary>
[Collection("sqlserver")]
public class PlatformListScopeTests
{
    private readonly SqlServerFixture _fixture;

    public PlatformListScopeTests(SqlServerFixture fixture) => _fixture = fixture;

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
        public bool IsAuthenticated { get; init; } = true;
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

    private async Task SeedImmeubleAsync(Guid projectId, string label)
    {
        await using var db = _fixture.CreateContext();
        db.Immeubles.Add(new Immeuble
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
        });
        await db.SaveChangesAsync();
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

    // ------------------------------------------------------------ GetAllProjects

    [Fact]
    public async Task GetAllProjects_ProjectAdmin_WithNoMembership_SeesNoProjects()
    {
        RequireDatabase();
        var ownProjectId = await SeedProjectAsync("F7-Projects-own");
        var foreignProjectId = await SeedProjectAsync("F7-Projects-foreign");
        await SeedMembershipAsync(ownProjectId, "f7-admin-1", RoleCodes.ProjectAdmin);

        var admin = new FakeCurrentUser { UserId = "f7-admin-1", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var handler = new GetAllProjectsHandler(
            new ProjectRepository(db),
            new LikedProjectRepository(db),
            admin,
            new ProjectScopeService(db, admin));

        var result = await handler.Handle(new GetAllProjectsQuery { PageSize = 100 }, CancellationToken.None);

        result.Data.Select(p => p.Id).Should().Contain(ownProjectId, "the admin's own membership must remain visible");
        result.Data.Select(p => p.Id).Should().NotContain(foreignProjectId,
            "a membership-less PROJECT_ADMIN must not see a project outside their perimeter (F7)");
    }

    [Fact]
    public async Task GetAllProjects_GlobalAdmin_SeesAllProjects()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("F7-Projects-global-admin");

        var globalAdmin = new FakeCurrentUser { UserId = "f7-global-admin-1", Roles = new[] { RoleCodes.GlobalAdmin } };
        var db = _fixture.CreateContext();
        var handler = new GetAllProjectsHandler(
            new ProjectRepository(db),
            new LikedProjectRepository(db),
            globalAdmin,
            new ProjectScopeService(db, globalAdmin));

        var result = await handler.Handle(new GetAllProjectsQuery { PageSize = 1000 }, CancellationToken.None);

        result.Data.Select(p => p.Id).Should().Contain(projectId, "a GLOBAL_ADMIN has no perimeter restriction");
    }

    [Fact]
    public async Task GetAllProjects_AnonymousVisitor_SeesAllProjects_PublicCatalogueUnaffected()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("F7-Projects-anonymous");

        var anonymous = new FakeCurrentUser { UserId = null, IsAuthenticated = false, Roles = Array.Empty<string>() };
        var db = _fixture.CreateContext();
        var handler = new GetAllProjectsHandler(
            new ProjectRepository(db),
            new LikedProjectRepository(db),
            anonymous,
            new ProjectScopeService(db, anonymous));

        var result = await handler.Handle(new GetAllProjectsQuery { PageSize = 1000 }, CancellationToken.None);

        result.Data.Select(p => p.Id).Should().Contain(projectId,
            "the public catalogue must remain unfiltered for anonymous visitors — only internal callers are scoped");
    }

    /// <summary>
    /// E2E QA finding (CRITICAL): GET /api/Projects is [AllowAnonymous] for the
    /// public catalogue, and ProjectResponse carries the project's staffing —
    /// every assigned agent's and notary's id, first name, last name, e-mail
    /// address and phone number. A visitor who called the route that feeds the
    /// public home page was handed the company's staff directory.
    /// </summary>
    [Fact]
    public async Task GetAllProjects_AnonymousVisitor_SeesNoStaffPii()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("PII-Projects-anonymous");
        await SeedMembershipAsync(projectId, "pii-agent-1", RoleCodes.SalesAgent);
        await SeedMembershipAsync(projectId, "pii-notary-1", RoleCodes.Notary);

        var anonymous = new FakeCurrentUser { UserId = null, IsAuthenticated = false, Roles = Array.Empty<string>() };
        var db = _fixture.CreateContext();
        var handler = new GetAllProjectsHandler(
            new ProjectRepository(db),
            new LikedProjectRepository(db),
            anonymous,
            new ProjectScopeService(db, anonymous));

        var result = await handler.Handle(new GetAllProjectsQuery { PageSize = 1000 }, CancellationToken.None);
        var project = result.Data.Single(p => p.Id == projectId);

        project.AssignedAgents.Should().BeEmpty("an anonymous visitor must never receive staff identities");
        project.AssignedNotaries.Should().BeEmpty("an anonymous visitor must never receive staff identities");
        project.AgentId.Should().BeNull();
        project.AgentPhoneNumber.Should().BeNull("a staff phone number is personal data, not catalogue data");

        // The commercial payload the public pages actually consume is untouched.
        project.Name.Should().Be("PII-Projects-anonymous");
    }

    [Fact]
    public async Task GetAllProjects_InternalCaller_StillSeesStaff()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("PII-Projects-internal");
        await SeedMembershipAsync(projectId, "pii-agent-2", RoleCodes.SalesAgent);

        var globalAdmin = new FakeCurrentUser { UserId = "pii-global-admin-1", Roles = new[] { RoleCodes.GlobalAdmin } };
        var db = _fixture.CreateContext();
        var handler = new GetAllProjectsHandler(
            new ProjectRepository(db),
            new LikedProjectRepository(db),
            globalAdmin,
            new ProjectScopeService(db, globalAdmin));

        var result = await handler.Handle(new GetAllProjectsQuery { PageSize = 1000 }, CancellationToken.None);
        var project = result.Data.Single(p => p.Id == projectId);

        project.AssignedAgents.Select(a => a.Id).Should().Contain("pii-agent-2",
            "the admin console's project list depends on the staffing block — stripping it for everyone would break assignment");
    }

    // ------------------------------------------------------------ GetAllImmeubles

    [Fact]
    public async Task GetAllImmeubles_ProjectAdmin_WithNoMembership_SeesNoBuildings()
    {
        RequireDatabase();
        var ownProjectId = await SeedProjectAsync("F7-Immeubles-own");
        var foreignProjectId = await SeedProjectAsync("F7-Immeubles-foreign");
        await SeedImmeubleAsync(ownProjectId, "own");
        await SeedImmeubleAsync(foreignProjectId, "foreign");
        await SeedMembershipAsync(ownProjectId, "f7-admin-2", RoleCodes.ProjectAdmin);

        var admin = new FakeCurrentUser { UserId = "f7-admin-2", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var handler = new GetAllImmeublesHandler(new ImmeubleRepository(db), admin, new ProjectScopeService(db, admin));

        var result = await handler.Handle(new GetAllImmeublesQuery { PageSize = 100 }, CancellationToken.None);

        result.Data.Select(i => i.ProjectId).Should().Contain(ownProjectId);
        result.Data.Select(i => i.ProjectId).Should().NotContain(foreignProjectId,
            "a membership-less PROJECT_ADMIN must not see a building outside their perimeter (F7)");
    }

    [Fact]
    public async Task GetAllImmeubles_GlobalAdmin_SeesAllBuildings()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("F7-Immeubles-global-admin");
        await SeedImmeubleAsync(projectId, "global-admin");

        var globalAdmin = new FakeCurrentUser { UserId = "f7-global-admin-2", Roles = new[] { RoleCodes.GlobalAdmin } };
        var db = _fixture.CreateContext();
        var handler = new GetAllImmeublesHandler(new ImmeubleRepository(db), globalAdmin, new ProjectScopeService(db, globalAdmin));

        var result = await handler.Handle(new GetAllImmeublesQuery { PageSize = 1000 }, CancellationToken.None);

        result.Data.Select(i => i.ProjectId).Should().Contain(projectId, "a GLOBAL_ADMIN has no perimeter restriction");
    }

    [Fact]
    public async Task GetAllImmeubles_AnonymousVisitor_SeesAllBuildings_PublicCatalogueUnaffected()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("F7-Immeubles-anonymous");
        await SeedImmeubleAsync(projectId, "anonymous");

        var anonymous = new FakeCurrentUser { UserId = null, IsAuthenticated = false, Roles = Array.Empty<string>() };
        var db = _fixture.CreateContext();
        var handler = new GetAllImmeublesHandler(new ImmeubleRepository(db), anonymous, new ProjectScopeService(db, anonymous));

        var result = await handler.Handle(new GetAllImmeublesQuery { PageSize = 1000 }, CancellationToken.None);

        result.Data.Select(i => i.ProjectId).Should().Contain(projectId,
            "the public catalogue must remain unfiltered for anonymous visitors — only internal callers are scoped");
    }

    // ------------------------------------------------------------ F2: CreateImmeuble validation

    [Fact]
    public async Task CreateImmeuble_WithoutSurfaceRangeFields_Succeeds()
    {
        // Regression F2: the admin creation UI (ProjectsListPage/ImmeublesListPage)
        // never collects MinSellableSurfaceRange/MaxSellableSurfaceRange, so both
        // wire in as 0. The original validator required MinSellableSurfaceRange >
        // 0 and MaxSellableSurfaceRange > Min, rejecting every real submission
        // with an unactionable "One or more validation errors occurred."
        RequireDatabase();
        var projectId = await SeedProjectAsync("F2-CreateImmeuble-no-surface-range");
        await SeedMembershipAsync(projectId, "f2-admin-1", RoleCodes.ProjectAdmin);

        var admin = new FakeCurrentUser { UserId = "f2-admin-1", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new CreateImmeubleHandler(
            new ImmeubleRepository(db),
            new ProjectRepository(db),
            new ImmeubleTrackingRepository(db),
            scope);

        var validator = new CreateImmeubleValidator();
        var command = new CreateImmeubleCommand
        {
            Name = "Building without surface range",
            ProjectId = projectId,
            Location = "Casablanca",
            Type = "Appartements",
            Images = string.Empty,
            Description = "test",
            MinSellableSurfaceRange = 0,
            MaxSellableSurfaceRange = 0,
        };

        var validation = await validator.ValidateAsync(command);
        validation.IsValid.Should().BeTrue(
            "MinSellableSurfaceRange/MaxSellableSurfaceRange are never collected by the creation UI and must not block it (F2)");

        var id = await handler.Handle(command, CancellationToken.None);
        id.Should().NotBeEmpty();
    }
}
