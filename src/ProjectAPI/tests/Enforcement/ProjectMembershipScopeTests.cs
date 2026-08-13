using FluentAssertions;
using Microsoft.Data.SqlClient;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// Phase 1 regression tests for the ProjectMembership scope model: a
/// membership only grants access while it is BOTH IsActive AND inside its
/// [ValidFrom, ValidUntil) window, and at most one ACTIVE row may exist per
/// (UserId, ProjectId, RoleCode) — enforced by a real database constraint,
/// not just application logic (see ProjectMembershipConfiguration).
/// </summary>
[Collection("sqlserver")]
public class ProjectMembershipScopeTests
{
    private readonly SqlServerFixture _fixture;

    public ProjectMembershipScopeTests(SqlServerFixture fixture) => _fixture = fixture;

    private void RequireDatabase()
    {
        if (!_fixture.Available)
        {
            throw new InvalidOperationException(
                "These tests assert real project-membership enforcement and cannot run without SQL Server. " +
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
        StatusGlobal = "DRAFT"
    };

    private async Task<Guid> SeedProjectAsync()
    {
        await using var db = _fixture.CreateContext();
        var project = NewProject("Membership test project");
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    /// <summary>
    /// ProjectMembership.UserId is a real FK into AspNetUsers (unlike the old
    /// ProjectAssignment.AgentId/NotaryId, which these tests could leave
    /// null) — every membership row needs a real user to point at.
    /// </summary>
    private async Task SeedUserAsync(string userId)
    {
        await using var db = _fixture.CreateContext();
        db.Users.Add(new User
        {
            Id = userId,
            UserName = userId,
            NormalizedUserName = userId.ToUpperInvariant(),
            Email = $"{userId}@test.local",
            NormalizedEmail = $"{userId}@test.local".ToUpperInvariant(),
            FirstName = "Test",
            LastName = "User",
            SecurityStamp = Guid.NewGuid().ToString()
        });
        await db.SaveChangesAsync();
    }

    private static ProjectMembership NewMembership(
        Guid projectId, string userId, string roleCode,
        DateTime validFrom, DateTime? validUntil, bool isActive) => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = projectId,
        UserId = userId,
        RoleCode = roleCode,
        ValidFrom = validFrom,
        ValidUntil = validUntil,
        IsActive = isActive,
        AssignedByUserId = null,
        AssignedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task ActiveMembership_WithinValidityWindow_GrantsAccess()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync();
        var userId = "sales-agent-1";
        await SeedUserAsync(userId);

        await using (var db = _fixture.CreateContext())
        {
            db.Set<ProjectMembership>().Add(NewMembership(
                projectId, userId, RoleCodes.SalesAgent,
                validFrom: DateTime.UtcNow.AddDays(-1), validUntil: null, isActive: true));
            await db.SaveChangesAsync();
        }

        var user = new FakeCurrentUser { UserId = userId, Roles = new[] { RoleCodes.SalesAgent } };
        var scope = new ProjectScopeService(_fixture.CreateContext(), user);

        var act = async () => await scope.EnsureProjectAccessAsync(projectId, CancellationToken.None);
        await act.Should().NotThrowAsync("an active membership inside its validity window must grant access");
    }

    [Fact]
    public async Task ExpiredMembership_ValidUntilInThePast_DoesNotGrantAccess()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync();
        var userId = "notary-expired";
        await SeedUserAsync(userId);

        await using (var db = _fixture.CreateContext())
        {
            db.Set<ProjectMembership>().Add(NewMembership(
                projectId, userId, RoleCodes.Notary,
                validFrom: DateTime.UtcNow.AddDays(-30), validUntil: DateTime.UtcNow.AddDays(-1), isActive: true));
            await db.SaveChangesAsync();
        }

        var user = new FakeCurrentUser { UserId = userId, Roles = new[] { RoleCodes.Notary } };
        var scope = new ProjectScopeService(_fixture.CreateContext(), user);

        var act = async () => await scope.EnsureProjectAccessAsync(projectId, CancellationToken.None);
        await act.Should().ThrowAsync<ProjectAPI.Api.Application.Common.Exceptions.BusinessRuleException>(
            "a membership whose ValidUntil has passed must no longer grant access, even though IsActive is still true");
    }

    [Fact]
    public async Task FutureMembership_ValidFromNotYetReached_DoesNotGrantAccess()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync();
        var userId = "technician-future";
        await SeedUserAsync(userId);

        await using (var db = _fixture.CreateContext())
        {
            db.Set<ProjectMembership>().Add(NewMembership(
                projectId, userId, RoleCodes.Technician,
                validFrom: DateTime.UtcNow.AddDays(7), validUntil: null, isActive: true));
            await db.SaveChangesAsync();
        }

        var user = new FakeCurrentUser { UserId = userId, Roles = new[] { RoleCodes.Technician } };
        var scope = new ProjectScopeService(_fixture.CreateContext(), user);

        var act = async () => await scope.EnsureProjectAccessAsync(projectId, CancellationToken.None);
        await act.Should().ThrowAsync<ProjectAPI.Api.Application.Common.Exceptions.BusinessRuleException>(
            "a membership that has not reached its ValidFrom date yet must not grant access");
    }

    [Fact]
    public async Task InactiveMembership_WithinValidityWindow_DoesNotGrantAccess()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync();
        var userId = "project-admin-deactivated";
        await SeedUserAsync(userId);

        await using (var db = _fixture.CreateContext())
        {
            db.Set<ProjectMembership>().Add(NewMembership(
                projectId, userId, RoleCodes.ProjectAdmin,
                validFrom: DateTime.UtcNow.AddDays(-10), validUntil: null, isActive: false));
            await db.SaveChangesAsync();
        }

        var user = new FakeCurrentUser { UserId = userId, Roles = new[] { RoleCodes.ProjectAdmin } };
        var scope = new ProjectScopeService(_fixture.CreateContext(), user);

        var act = async () => await scope.EnsureProjectAccessAsync(projectId, CancellationToken.None);
        await act.Should().ThrowAsync<ProjectAPI.Api.Application.Common.Exceptions.BusinessRuleException>(
            "IsActive=false must deny access even while the validity window is otherwise current — " +
            "the administrative switch and the time window are independent checks, both required");
    }

    [Fact]
    public async Task GlobalAdmin_BypassesMembershipEntirely()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync();

        // No ProjectMembership row at all for this user — GLOBAL_ADMIN must
        // still pass, since GetScopedProjectIdsAsync short-circuits to null
        // (unrestricted) before ever querying ProjectMembership.
        var globalAdmin = new FakeCurrentUser { UserId = "global-1", Roles = new[] { RoleCodes.GlobalAdmin } };
        var scope = new ProjectScopeService(_fixture.CreateContext(), globalAdmin);

        var act = async () => await scope.EnsureProjectAccessAsync(projectId, CancellationToken.None);
        await act.Should().NotThrowAsync("GLOBAL_ADMIN is unrestricted regardless of membership rows");
    }

    /// <summary>
    /// The filtered unique index (IX_ProjectMemberships_ActivePerUserProjectRole)
    /// is the database-level backstop for "at most one active membership per
    /// (UserId, ProjectId, RoleCode)" — this proves it actually fires, not
    /// just that application code happens to avoid violating it.
    /// </summary>
    [Fact]
    public async Task DatabaseRejectsASecondActiveMembership_ForTheSameUserProjectRole()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync();
        var userId = "sales-agent-dup";
        await SeedUserAsync(userId);

        await using (var first = _fixture.CreateContext())
        {
            first.Set<ProjectMembership>().Add(NewMembership(
                projectId, userId, RoleCodes.SalesAgent,
                validFrom: DateTime.UtcNow.AddDays(-1), validUntil: null, isActive: true));
            await first.SaveChangesAsync();
        }

        Exception? violation = null;
        try
        {
            await using var second = _fixture.CreateContext();
            second.Set<ProjectMembership>().Add(NewMembership(
                projectId, userId, RoleCodes.SalesAgent,
                validFrom: DateTime.UtcNow, validUntil: null, isActive: true));
            await second.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            violation = ex;
        }

        violation.Should().NotBeNull(
            "a second ACTIVE membership for the same (UserId, ProjectId, RoleCode) must be rejected by the database");

        var sql = Unwrap(violation!);
        sql.Should().NotBeNull("the refusal must come from SQL Server, not application code");
        new[] { 2601, 2627 }.Should().Contain(sql!.Number, "2601/2627 are the unique-violation numbers");
        sql.Message.Should().Contain("IX_ProjectMemberships_ActivePerUserProjectRole",
            "the violation must name this specific index — that's the constraint under test");
    }

    /// <summary>
    /// A second membership for the same key is fine once the first is no
    /// longer active — deactivating then inserting is the supported
    /// reassignment path, and the filtered index (WHERE IsActive = 1) must
    /// not block it.
    /// </summary>
    [Fact]
    public async Task InactiveHistoricalMembership_DoesNotBlockANewActiveOneForTheSameKey()
    {
        RequireDatabase();
        var projectId = await SeedProjectAsync();
        var userId = "notary-reassigned";
        await SeedUserAsync(userId);

        await using (var db = _fixture.CreateContext())
        {
            db.Set<ProjectMembership>().Add(NewMembership(
                projectId, userId, RoleCodes.Notary,
                validFrom: DateTime.UtcNow.AddDays(-60), validUntil: DateTime.UtcNow.AddDays(-30), isActive: false));
            db.Set<ProjectMembership>().Add(NewMembership(
                projectId, userId, RoleCodes.Notary,
                validFrom: DateTime.UtcNow.AddDays(-1), validUntil: null, isActive: true));

            var save = async () => await db.SaveChangesAsync();
            await save.Should().NotThrowAsync(
                "the filtered index only constrains IsActive=1 rows — an inactive historical row must never block a new active one");
        }
    }

    private static SqlException? Unwrap(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql) return sql;
        }
        return null;
    }
}
