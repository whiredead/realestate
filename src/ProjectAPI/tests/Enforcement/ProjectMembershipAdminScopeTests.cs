using ProjectAPI.Domain.Identity.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.ProjectMemberships.CreateProjectMembership;
using ProjectAPI.Api.Application.ProjectMemberships.GetAllProjectMemberships;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Repositories;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// Phase 1 Checkpoint 3 — the new ProjectMembershipController/handlers must
/// enforce the same project perimeter as the legacy ProjectAssignment ones
/// (see ProjectAdminScopeTests): a PROJECT_ADMIN scoped to one project must
/// never create or see memberships for another. Also covers the "requested
/// role must match the target account's actual platform role" guard.
/// </summary>
[Collection("sqlserver")]
public class ProjectMembershipAdminScopeTests
{
    private readonly SqlServerFixture _fixture;

    public ProjectMembershipAdminScopeTests(SqlServerFixture fixture) => _fixture = fixture;

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

    private static Project NewProject(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Location = "Casablanca",
        Address = "1 rue de test",
        Description = "seed",
        Module3DLink = string.Empty,
        StatusGlobal = "SUR_PLAN"
    };

    private async Task<Guid> SeedProjectAsync(string name)
    {
        await using var db = _fixture.CreateContext();
        var project = NewProject(name);
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    /// <summary>
    /// Seeds a real AspNetUsers row and, if roleCode is supplied, a real
    /// AspNetUserRoles row via UserManager — CreateProjectMembershipHandler
    /// checks the target's actual roles, so a plain seeded row with no role
    /// would always fail that check.
    /// </summary>
    private async Task SeedUserAsync(string userId, string? roleCode = null)
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
            if (roleCode != null && !await roleManager.RoleExistsAsync(roleCode))
            {
                await roleManager.CreateAsync(new Role { Id = Guid.NewGuid().ToString(), Name = roleCode, DisplayName = roleCode });
            }

            (await userManager.CreateAsync(user)).Succeeded.Should().BeTrue("test user seeding must succeed");

            if (roleCode != null)
            {
                (await userManager.AddToRoleAsync(user, roleCode)).Succeeded.Should().BeTrue("test role seeding must succeed");
            }
        }
    }

    [Fact]
    public async Task ProjectAdmin_CannotCreateMembership_ForProjectOutsideOwnPerimeter()
    {
        RequireDatabase();
        var other = await SeedProjectAsync("Other project");
        await SeedUserAsync("grantee-1", RoleCodes.SalesAgent);

        // No ProjectMembership row for this admin — scope resolves to empty,
        // so "other" (or any project) must be rejected. This is the same
        // regression ProjectAdminScopeTests guards for the legacy handlers.
        var admin = new FakeCurrentUser { UserId = "membership-admin-1", Roles = new[] { RoleCodes.ProjectAdmin } };
        var scope = new ProjectScopeService(_fixture.CreateContext(), admin);
        var handler = new CreateProjectMembershipHandler(
            _fixture.CreateContext(), _fixture.CreateUserManager().UserManager, scope, admin);

        var command = new CreateProjectMembershipCommand
        {
            ProjectId = other,
            UserId = "grantee-1",
            RoleCode = RoleCodes.SalesAgent
        };

        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>(
            "CreateProjectMembershipHandler must reject a project admin acting outside their perimeter");
    }

    [Fact]
    public async Task GlobalAdmin_CanCreateMembership_ForAnyProject()
    {
        RequireDatabase();
        var other = await SeedProjectAsync("Other project 2");
        await SeedUserAsync("grantee-2", RoleCodes.Notary);
        await SeedUserAsync("global-membership-admin");

        var globalAdmin = new FakeCurrentUser { UserId = "global-membership-admin", Roles = new[] { RoleCodes.GlobalAdmin } };
        var scope = new ProjectScopeService(_fixture.CreateContext(), globalAdmin);
        var handler = new CreateProjectMembershipHandler(
            _fixture.CreateContext(), _fixture.CreateUserManager().UserManager, scope, globalAdmin);

        var command = new CreateProjectMembershipCommand
        {
            ProjectId = other,
            UserId = "grantee-2",
            RoleCode = RoleCodes.Notary
        };

        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().NotThrowAsync("GLOBAL_ADMIN is unrestricted across every project perimeter");
    }

    [Fact]
    public async Task CreateMembership_RejectsGlobalAdminRoleCode()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("Project for role validation");
        await SeedUserAsync("grantee-3", RoleCodes.GlobalAdmin);

        var globalAdmin = new FakeCurrentUser { UserId = "global-membership-admin-2", Roles = new[] { RoleCodes.GlobalAdmin } };
        var scope = new ProjectScopeService(_fixture.CreateContext(), globalAdmin);
        var handler = new CreateProjectMembershipHandler(
            _fixture.CreateContext(), _fixture.CreateUserManager().UserManager, scope, globalAdmin);

        var command = new CreateProjectMembershipCommand
        {
            ProjectId = projectId,
            UserId = "grantee-3",
            RoleCode = RoleCodes.GlobalAdmin
        };

        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ValidationException>(
            "GLOBAL_ADMIN is platform-wide and unrestricted — a per-project membership row for it would be meaningless and must be rejected");
    }

    /// <summary>
    /// The target account's actual platform role must match the requested
    /// membership RoleCode. An account that only holds SALES_AGENT must be
    /// rejected when an admin tries to grant it a NOTARY membership — a
    /// membership grants project SCOPE for a role the account already has,
    /// it does not grant the role itself.
    /// </summary>
    [Fact]
    public async Task CreateMembership_RejectsRoleCodeTheTargetUserDoesNotHold()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("Project for role-mismatch check");
        await SeedUserAsync("sales-agent-only", RoleCodes.SalesAgent);

        var admin = new FakeCurrentUser { UserId = "membership-admin-3", Roles = new[] { RoleCodes.GlobalAdmin } };
        var scope = new ProjectScopeService(_fixture.CreateContext(), admin);
        var handler = new CreateProjectMembershipHandler(
            _fixture.CreateContext(), _fixture.CreateUserManager().UserManager, scope, admin);

        var command = new CreateProjectMembershipCommand
        {
            ProjectId = projectId,
            UserId = "sales-agent-only",
            RoleCode = RoleCodes.Notary
        };

        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ValidationException>(
            "the account only holds SALES_AGENT — granting a NOTARY membership must be rejected, " +
            "not silently create project-scoped notary access for a non-notary account");
    }

    /// <summary>
    /// The mirror image of the rejection test: a membership succeeds when
    /// the requested role matches a role the account actually holds.
    /// </summary>
    [Fact]
    public async Task CreateMembership_SucceedsWhenRoleCodeMatchesTheTargetUsersActualRole()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("Project for role-match check");
        await SeedUserAsync("technician-real", RoleCodes.Technician);
        await SeedUserAsync("membership-admin-4");

        var admin = new FakeCurrentUser { UserId = "membership-admin-4", Roles = new[] { RoleCodes.GlobalAdmin } };
        var scope = new ProjectScopeService(_fixture.CreateContext(), admin);
        var handler = new CreateProjectMembershipHandler(
            _fixture.CreateContext(), _fixture.CreateUserManager().UserManager, scope, admin);

        var command = new CreateProjectMembershipCommand
        {
            ProjectId = projectId,
            UserId = "technician-real",
            RoleCode = RoleCodes.Technician
        };

        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().NotThrowAsync("the account holds TECHNICIAN, matching the requested membership role");
    }

    [Fact]
    public async Task ProjectAdmin_ListingMemberships_NeverSeesMembershipsOutsideOwnPerimeter()
    {
        RequireDatabase();
        var other = await SeedProjectAsync("Other project 3");
        await SeedUserAsync("grantee-4", RoleCodes.Technician);

        Guid membershipId;
        await using (var db = _fixture.CreateContext())
        {
            var membership = new ProjectMembership
            {
                Id = Guid.NewGuid(),
                ProjectId = other,
                UserId = "grantee-4",
                RoleCode = RoleCodes.Technician,
                ValidFrom = DateTime.UtcNow.AddDays(-1),
                ValidUntil = null,
                IsActive = true,
                AssignedByUserId = null,
                AssignedAt = DateTime.UtcNow
            };
            db.Set<ProjectMembership>().Add(membership);
            await db.SaveChangesAsync();
            membershipId = membership.Id;
        }

        var admin = new FakeCurrentUser { UserId = "membership-admin-2", Roles = new[] { RoleCodes.ProjectAdmin } };
        var repository = new ProjectMembershipRepository(_fixture.CreateContext());
        var scope = new ProjectScopeService(_fixture.CreateContext(), admin);
        var handler = new GetAllProjectMembershipsHandler(repository, scope);

        var result = await handler.Handle(
            new GetAllProjectMembershipsQuery { PageNumber = 1, PageSize = 50 },
            CancellationToken.None);

        result.Data.Should().NotContain(m => m.Id == membershipId,
            "a project admin outside this project's perimeter must never see its memberships in a listing");
    }
}
