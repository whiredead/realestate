using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectAPI.Api.Application.Agents.CreateAgentBlock;
using ProjectAPI.Api.Application.Agents.DeleteAgentBlock;
using ProjectAPI.Api.Application.Agents.GetAgentBlocks;
using ProjectAPI.Api.Application.Agents.GetAgentWeeklyAvailability;
using ProjectAPI.Api.Application.Agents.SetAgentWeeklyAvailability;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Repositories;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// §6.3 — "Un agent gère uniquement son propre calendrier" — the direct
/// mirror of the notary-calendar gap: every Agents\ handler took AgentId
/// from the {agentId} route segment with no check it matched the caller. A
/// SALES_AGENT signed in as themselves could read, overwrite, or delete any
/// other agent's blocks and weekly schedule just by changing the URL.
/// </summary>
[Collection("sqlserver")]
public class AgentCalendarScopeTests
{
    private readonly SqlServerFixture _fixture;

    public AgentCalendarScopeTests(SqlServerFixture fixture) => _fixture = fixture;

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

    private async Task SeedAgentAsync(string userId)
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
                LastName = "Agent",
                SecurityStamp = Guid.NewGuid().ToString()
            };

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roleManager.RoleExistsAsync(RoleCodes.SalesAgent))
            {
                await roleManager.CreateAsync(new IdentityRole(RoleCodes.SalesAgent));
            }

            (await userManager.CreateAsync(user)).Succeeded.Should().BeTrue("test agent seeding must succeed");
            (await userManager.AddToRoleAsync(user, RoleCodes.SalesAgent)).Succeeded.Should().BeTrue("test role seeding must succeed");
        }
    }

    private async Task<Guid> SeedBlockAsync(string agentId)
    {
        await using var db = _fixture.CreateContext();
        var block = new AgentBlock
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            Start = DateTime.UtcNow.AddDays(5),
            End = DateTime.UtcNow.AddDays(5).AddHours(2),
            Reason = "Congé"
        };
        db.Set<AgentBlock>().Add(block);
        await db.SaveChangesAsync();
        return block.Id;
    }

    private async Task SeedWeeklyAvailabilityAsync(string agentId)
    {
        await using var db = _fixture.CreateContext();
        db.Set<AgentWeeklyAvailability>().Add(new AgentWeeklyAvailability
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(17)
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateBlock_AgentCaller_CannotBlockAnotherAgentsCalendar()
    {
        RequireDatabase();
        await SeedAgentAsync("agt-cal-owner-1");
        await SeedAgentAsync("agt-cal-intruder-1");

        var intruder = new FakeCurrentUser { UserId = "agt-cal-intruder-1", Roles = new[] { RoleCodes.SalesAgent } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, intruder);
        var handler = new CreateAgentBlockHandler(new AgentBlockRepository(db), new AppointmentRepository(db), scope);

        var command = new CreateAgentBlockCommand
        {
            AgentId = "agt-cal-owner-1",
            Start = DateTime.UtcNow.AddDays(3),
            End = DateTime.UtcNow.AddDays(3).AddHours(1),
            Reason = "Hostile block"
        };

        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>(
            "a SALES_AGENT must not be able to create a block on another agent's calendar");
    }

    [Fact]
    public async Task CreateBlock_AgentCaller_CanBlockOwnCalendar()
    {
        RequireDatabase();
        await SeedAgentAsync("agt-cal-owner-2");

        var owner = new FakeCurrentUser { UserId = "agt-cal-owner-2", Roles = new[] { RoleCodes.SalesAgent } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, owner);
        var handler = new CreateAgentBlockHandler(new AgentBlockRepository(db), new AppointmentRepository(db), scope);

        var command = new CreateAgentBlockCommand
        {
            AgentId = "agt-cal-owner-2",
            Start = DateTime.UtcNow.AddDays(3),
            End = DateTime.UtcNow.AddDays(3).AddHours(1),
            Reason = "Own block"
        };

        var result = await handler.Handle(command, CancellationToken.None);
        result.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateBlock_AdminCaller_CanBlockAnyAgentsCalendar()
    {
        RequireDatabase();
        await SeedAgentAsync("agt-cal-target-1");

        var admin = new FakeCurrentUser { UserId = "agt-cal-admin-1", Roles = new[] { RoleCodes.GlobalAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new CreateAgentBlockHandler(new AgentBlockRepository(db), new AppointmentRepository(db), scope);

        var command = new CreateAgentBlockCommand
        {
            AgentId = "agt-cal-target-1",
            Start = DateTime.UtcNow.AddDays(3),
            End = DateTime.UtcNow.AddDays(3).AddHours(1),
            Reason = "Admin-managed block"
        };

        var result = await handler.Handle(command, CancellationToken.None);
        result.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DeleteBlock_AgentCaller_CannotDeleteAnotherAgentsBlock()
    {
        RequireDatabase();
        await SeedAgentAsync("agt-cal-owner-3");
        await SeedAgentAsync("agt-cal-intruder-3");
        var blockId = await SeedBlockAsync("agt-cal-owner-3");

        var intruder = new FakeCurrentUser { UserId = "agt-cal-intruder-3", Roles = new[] { RoleCodes.SalesAgent } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, intruder);
        var handler = new DeleteAgentBlockHandler(new AgentBlockRepository(db), scope);

        var act = async () => await handler.Handle(
            new DeleteAgentBlockCommand { AgentId = "agt-cal-owner-3", BlockId = blockId },
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>(
            "a SALES_AGENT must not be able to delete another agent's block");

        await using var verify = _fixture.CreateContext();
        (await verify.Set<AgentBlock>().AnyAsync(b => b.Id == blockId)).Should().BeTrue(
            "the block must survive a denied cross-agent delete attempt");
    }

    [Fact]
    public async Task GetBlocks_AgentCaller_CannotReadAnotherAgentsBlocks()
    {
        RequireDatabase();
        await SeedAgentAsync("agt-cal-owner-4");
        await SeedAgentAsync("agt-cal-intruder-4");
        await SeedBlockAsync("agt-cal-owner-4");

        var intruder = new FakeCurrentUser { UserId = "agt-cal-intruder-4", Roles = new[] { RoleCodes.SalesAgent } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, intruder);
        var handler = new GetAgentBlocksHandler(new AgentBlockRepository(db), scope);

        var act = async () => await handler.Handle(
            new GetAgentBlocksQuery { AgentId = "agt-cal-owner-4" },
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>(
            "a SALES_AGENT must not be able to read another agent's blocks");
    }

    [Fact]
    public async Task SetWeeklyAvailability_AgentCaller_CannotOverwriteAnotherAgentsSchedule()
    {
        RequireDatabase();
        await SeedAgentAsync("agt-cal-owner-5");
        await SeedAgentAsync("agt-cal-intruder-5");
        await SeedWeeklyAvailabilityAsync("agt-cal-owner-5");

        var intruder = new FakeCurrentUser { UserId = "agt-cal-intruder-5", Roles = new[] { RoleCodes.SalesAgent } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, intruder);
        var handler = new SetAgentWeeklyAvailabilityHandler(new AgentWeeklyAvailabilityRepository(db), scope);

        var command = new SetAgentWeeklyAvailabilityCommand
        {
            AgentId = "agt-cal-owner-5",
            Slots = new List<WeeklyAvailabilitySlot>
            {
                new() { DayOfWeek = DayOfWeek.Tuesday, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12) }
            }
        };

        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>(
            "a SALES_AGENT must not be able to replace another agent's weekly schedule");

        await using var verify = _fixture.CreateContext();
        var ownerSlots = await verify.Set<AgentWeeklyAvailability>().Where(w => w.AgentId == "agt-cal-owner-5").ToListAsync();
        ownerSlots.Should().ContainSingle(w => w.DayOfWeek == DayOfWeek.Monday,
            "the owner's original Monday slot must survive a denied cross-agent overwrite");
    }

    [Fact]
    public async Task GetWeeklyAvailability_AgentCaller_CannotReadAnotherAgentsSchedule()
    {
        RequireDatabase();
        await SeedAgentAsync("agt-cal-owner-7");
        await SeedAgentAsync("agt-cal-intruder-7");
        await SeedWeeklyAvailabilityAsync("agt-cal-owner-7");

        var intruder = new FakeCurrentUser { UserId = "agt-cal-intruder-7", Roles = new[] { RoleCodes.SalesAgent } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, intruder);
        var handler = new GetAgentWeeklyAvailabilityHandler(new AgentWeeklyAvailabilityRepository(db), scope);

        var act = async () => await handler.Handle(
            new GetAgentWeeklyAvailabilityQuery { AgentId = "agt-cal-owner-7" },
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>(
            "a SALES_AGENT must not be able to read another agent's weekly availability");
    }

    [Fact]
    public async Task GetWeeklyAvailability_AdminCaller_CanReadAnyAgentsSchedule()
    {
        RequireDatabase();
        await SeedAgentAsync("agt-cal-target-2");
        await SeedWeeklyAvailabilityAsync("agt-cal-target-2");

        var admin = new FakeCurrentUser { UserId = "agt-cal-admin-2", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new GetAgentWeeklyAvailabilityHandler(new AgentWeeklyAvailabilityRepository(db), scope);

        var result = await handler.Handle(
            new GetAgentWeeklyAvailabilityQuery { AgentId = "agt-cal-target-2" },
            CancellationToken.None);

        result.Should().ContainSingle(s => s.DayOfWeek == DayOfWeek.Monday);
    }
}
