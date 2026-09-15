using ProjectAPI.Domain.Identity.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.ProjectAssignments.CreateProjectAssignment;
using ProjectAPI.Api.Application.ProjectAssignments.GetAllProjectAssignments;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Repositories;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// Phase 0/Phase 1 regression tests for the LEGACY ProjectAssignment
/// endpoints: a PROJECT_ADMIN must never be able to act on a project outside
/// their own perimeter, even though the controller-level
/// [Authorize(Roles = RoleGroups.Admins)] gate lets both PROJECT_ADMIN and
/// GLOBAL_ADMIN reach these handlers.
///
/// Phase 1 rewrote these handlers so they no longer touch the
/// ProjectAssignments table at all — every write and read here goes through
/// ProjectMembership, the only source of truth (see
/// CreateProjectAssignmentHandler's doc comment). These tests assert BOTH
/// the scope behaviour AND that fact directly: a legacy Create leaves no
/// ProjectAssignments row behind, and a legacy Create/GetAll is provably
/// reading/writing ProjectMembership by seeding data there and nowhere else.
/// </summary>
[Collection("sqlserver")]
public class ProjectAdminScopeTests
{
    private readonly SqlServerFixture _fixture;

    public ProjectAdminScopeTests(SqlServerFixture fixture) => _fixture = fixture;

    private void RequireDatabase()
    {
        if (!_fixture.Available)
        {
            throw new InvalidOperationException(
                "These tests assert real project-scope enforcement and cannot run without SQL Server. " +
                $"Connection failed: {_fixture.UnavailableReason}");
        }
    }

    /// <summary>A fake authenticated caller — avoids standing up HttpContext/ClaimsPrincipal for a pure role+id check.</summary>
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
    /// AspNetUserRoles row via UserManager — CreateProjectAssignmentHandler
    /// (like CreateProjectMembershipHandler) now checks the target's actual
    /// roles before granting project scope for that role.
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

    /// <summary>
    /// A project admin scoped to nothing must not be able to create an
    /// assignment for "other" — this is the exact escalation the controller's
    /// role-only [Authorize] gate used to allow.
    /// </summary>
    [Fact]
    public async Task ProjectAdmin_CannotCreateAssignment_ForProjectOutsideOwnPerimeter()
    {
        RequireDatabase();
        var other = await SeedProjectAsync("Other project");
        await SeedUserAsync("legacy-agent-1", RoleCodes.SalesAgent);

        var admin = new FakeCurrentUser { UserId = "legacy-admin-1", Roles = new[] { RoleCodes.ProjectAdmin } };

        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new CreateProjectAssignmentHandler(db, _fixture.CreateUserManager().UserManager, scope, admin);

        var command = new CreateProjectAssignmentCommand
        {
            ProjectId = other,
            AgentId = "legacy-agent-1",
            NotaryId = null,
            IsActive = true
        };

        var createForOther = async () => await handler.Handle(command, CancellationToken.None);
        await createForOther.Should().ThrowAsync<BusinessRuleException>(
            "CreateProjectAssignmentHandler must call EnsureProjectAccessAsync before persisting, " +
            "and a project admin outside a project's perimeter must be rejected");
    }

    /// <summary>
    /// A GLOBAL_ADMIN has no perimeter restriction (GetScopedProjectIdsAsync
    /// returns null) — the same "other" project that rejects a project admin
    /// must be reachable for a global admin. Also proves the legacy Create
    /// endpoint persists into ProjectMembership, not ProjectAssignments.
    /// </summary>
    [Fact]
    public async Task GlobalAdmin_CanCreateAssignment_ForAnyProject_AndItLandsInProjectMembership()
    {
        RequireDatabase();
        var other = await SeedProjectAsync("Other project 2");
        await SeedUserAsync("legacy-agent-2", RoleCodes.SalesAgent);
        await SeedUserAsync("legacy-global-admin");

        var globalAdmin = new FakeCurrentUser { UserId = "legacy-global-admin", Roles = new[] { RoleCodes.GlobalAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, globalAdmin);
        var handler = new CreateProjectAssignmentHandler(db, _fixture.CreateUserManager().UserManager, scope, globalAdmin);

        var command = new CreateProjectAssignmentCommand
        {
            ProjectId = other,
            AgentId = "legacy-agent-2",
            NotaryId = null,
            IsActive = true
        };

        var response = await handler.Handle(command, CancellationToken.None);

        await using var verify = _fixture.CreateContext();

        var membership = await verify.Set<ProjectMembership>().FirstOrDefaultAsync(m => m.Id == response.Id);
        membership.Should().NotBeNull("the legacy Create endpoint must write into ProjectMembership");
        membership!.ProjectId.Should().Be(other);
        membership.UserId.Should().Be("legacy-agent-2");
        membership.RoleCode.Should().Be(RoleCodes.SalesAgent);

        var legacyRowCount = await verify.Set<ProjectAssignment>().CountAsync(a => a.ProjectId == other);
        legacyRowCount.Should().Be(0,
            "the legacy Create endpoint must not write a second, independent ProjectAssignments row — " +
            "ProjectMembership is the only writable source of truth");
    }

    /// <summary>
    /// The legacy Create endpoint must enforce the same role-match guard as
    /// CreateProjectMembershipHandler: AgentId must actually hold SALES_AGENT
    /// on their account.
    /// </summary>
    [Fact]
    public async Task CreateAssignment_RejectsAgentIdThatDoesNotHoldSalesAgentRole()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("Project for legacy role-mismatch check");
        await SeedUserAsync("legacy-notary-only", RoleCodes.Notary);

        var globalAdmin = new FakeCurrentUser { UserId = "legacy-global-admin-3", Roles = new[] { RoleCodes.GlobalAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, globalAdmin);
        var handler = new CreateProjectAssignmentHandler(db, _fixture.CreateUserManager().UserManager, scope, globalAdmin);

        var command = new CreateProjectAssignmentCommand
        {
            ProjectId = projectId,
            AgentId = "legacy-notary-only",
            NotaryId = null,
            IsActive = true
        };

        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ValidationException>(
            "the account only holds NOTARY — granting it a SALES_AGENT assignment via the legacy endpoint must be rejected");
    }

    /// <summary>
    /// GetAllProjectAssignmentsHandler must filter results to the caller's
    /// perimeter, not merely gate the endpoint by role. A membership that
    /// belongs to a project outside a project admin's scope must never appear
    /// in their listing — and the listing must be sourced from
    /// ProjectMembership, never from a legacy ProjectAssignments row that
    /// happens to exist independently.
    /// </summary>
    [Fact]
    public async Task ProjectAdmin_ListingAssignments_NeverSeesAssignmentsOutsideOwnPerimeter()
    {
        RequireDatabase();
        var other = await SeedProjectAsync("Other project 3");
        await SeedUserAsync("legacy-agent-3", RoleCodes.SalesAgent);

        Guid membershipId;
        await using (var db = _fixture.CreateContext())
        {
            var membership = new ProjectMembership
            {
                Id = Guid.NewGuid(),
                ProjectId = other,
                UserId = "legacy-agent-3",
                RoleCode = RoleCodes.SalesAgent,
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

        // No ProjectMembership row for this admin means their scope resolves
        // to the empty set — proving the listing is filtered (empty), not
        // unfiltered (both projects' memberships).
        var admin = new FakeCurrentUser { UserId = "legacy-admin-2", Roles = new[] { RoleCodes.ProjectAdmin } };
        var repo = new ProjectMembershipRepository(_fixture.CreateContext());
        var scope = new ProjectScopeService(_fixture.CreateContext(), admin);
        var handler = new GetAllProjectAssignmentsHandler(repo, scope);

        var result = await handler.Handle(
            new GetAllProjectAssignmentsQuery { PageNumber = 1, PageSize = 50 },
            CancellationToken.None);

        result.Data.Should().NotContain(a => a.Id == membershipId,
            "a project admin outside this project's perimeter must never see its memberships in a listing");
    }

    /// <summary>
    /// A global admin can list across every project — and the returned Id
    /// must be the ProjectMembership.Id (there is no ProjectAssignments row
    /// backing it any more), proving GetAll reads from the new model.
    /// </summary>
    [Fact]
    public async Task GetAllAssignments_ReturnsProjectMembershipIds_NotLegacyRows()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync("Project for id check");
        await SeedUserAsync("legacy-agent-4", RoleCodes.SalesAgent);

        Guid membershipId;
        await using (var db = _fixture.CreateContext())
        {
            var membership = new ProjectMembership
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                UserId = "legacy-agent-4",
                RoleCode = RoleCodes.SalesAgent,
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

        var globalAdmin = new FakeCurrentUser { UserId = "legacy-global-admin-2", Roles = new[] { RoleCodes.GlobalAdmin } };
        var repo = new ProjectMembershipRepository(_fixture.CreateContext());
        var scope = new ProjectScopeService(_fixture.CreateContext(), globalAdmin);
        var handler = new GetAllProjectAssignmentsHandler(repo, scope);

        var result = await handler.Handle(
            new GetAllProjectAssignmentsQuery { ProjectId = projectId, PageNumber = 1, PageSize = 50 },
            CancellationToken.None);

        result.Data.Should().ContainSingle(a => a.Id == membershipId && a.AgentId == "legacy-agent-4",
            "the legacy GetAll endpoint must expose ProjectMembership rows under their real ProjectMembership.Id");
    }
}
