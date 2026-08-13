using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Notary.CreateNotaryBlocks;
using ProjectAPI.Api.Application.Notary.DeleteNotaryBlock;
using ProjectAPI.Api.Application.Notary.GetNotaryBlocks;
using ProjectAPI.Api.Application.Notary.GetNotaryWeeklyAvailability;
using ProjectAPI.Api.Application.Notary.SetNotaryWeeklyAvailability;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Repositories;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// §6.3 — "Un notaire gère uniquement son propre calendrier." Before this
/// work, NotaryId on every block/weekly-availability handler came straight
/// from the {notaryId} route segment with no check it matched the caller: a
/// NOTARY signed in as themselves could read, overwrite or delete any other
/// notary's blocks and weekly schedule just by changing the URL. Admins are
/// unaffected — the controller's [Authorize(Roles = AdminsNotary)] gate is
/// correct for them, it's the NOTARY half that had no ownership check at all.
/// </summary>
[Collection("sqlserver")]
public class NotaryCalendarScopeTests
{
    private readonly SqlServerFixture _fixture;

    public NotaryCalendarScopeTests(SqlServerFixture fixture) => _fixture = fixture;

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

    private async Task SeedNotaryAsync(string userId)
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
                LastName = "Notary",
                SecurityStamp = Guid.NewGuid().ToString()
            };

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roleManager.RoleExistsAsync(RoleCodes.Notary))
            {
                await roleManager.CreateAsync(new IdentityRole(RoleCodes.Notary));
            }

            (await userManager.CreateAsync(user)).Succeeded.Should().BeTrue("test notary seeding must succeed");
            (await userManager.AddToRoleAsync(user, RoleCodes.Notary)).Succeeded.Should().BeTrue("test role seeding must succeed");
        }
    }

    private async Task<Guid> SeedBlockAsync(string notaryId)
    {
        await using var db = _fixture.CreateContext();
        var block = new NotaryBlock
        {
            Id = Guid.NewGuid(),
            NotaryId = notaryId,
            Start = DateTime.UtcNow.AddDays(5),
            End = DateTime.UtcNow.AddDays(5).AddHours(2),
            Reason = "Congé"
        };
        db.Set<NotaryBlock>().Add(block);
        await db.SaveChangesAsync();
        return block.Id;
    }

    private async Task SeedWeeklyAvailabilityAsync(string notaryId)
    {
        await using var db = _fixture.CreateContext();
        db.Set<WeeklyAvailability>().Add(new WeeklyAvailability
        {
            Id = Guid.NewGuid(),
            NotaryId = notaryId,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(17)
        });
        await db.SaveChangesAsync();
    }

    // ------------------------------------------------------------ CreateNotaryBlock

    [Fact]
    public async Task CreateBlock_NotaryCaller_CannotBlockAnotherNotarysCalendar()
    {
        RequireDatabase();
        await SeedNotaryAsync("cal-notary-owner-1");
        await SeedNotaryAsync("cal-notary-intruder-1");

        var intruder = new FakeCurrentUser { UserId = "cal-notary-intruder-1", Roles = new[] { RoleCodes.Notary } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, intruder);
        var handler = new CreateNotaryBlockHandler(
            new NotaryBlockRepository(db),
            new NotaryAppointmentRepository(db),
            scope);

        var command = new CreateNotaryBlockCommand
        {
            NotaryId = "cal-notary-owner-1",
            Start = DateTime.UtcNow.AddDays(3),
            End = DateTime.UtcNow.AddDays(3).AddHours(1),
            Reason = "Hostile block"
        };

        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>(
            "a NOTARY must not be able to create a block on another notary's calendar");
    }

    [Fact]
    public async Task CreateBlock_NotaryCaller_CanBlockOwnCalendar()
    {
        RequireDatabase();
        await SeedNotaryAsync("cal-notary-owner-2");

        var owner = new FakeCurrentUser { UserId = "cal-notary-owner-2", Roles = new[] { RoleCodes.Notary } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, owner);
        var handler = new CreateNotaryBlockHandler(
            new NotaryBlockRepository(db),
            new NotaryAppointmentRepository(db),
            scope);

        var command = new CreateNotaryBlockCommand
        {
            NotaryId = "cal-notary-owner-2",
            Start = DateTime.UtcNow.AddDays(3),
            End = DateTime.UtcNow.AddDays(3).AddHours(1),
            Reason = "Own block"
        };

        var result = await handler.Handle(command, CancellationToken.None);
        result.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateBlock_AdminCaller_CanBlockAnyNotarysCalendar()
    {
        RequireDatabase();
        await SeedNotaryAsync("cal-notary-target-1");

        var admin = new FakeCurrentUser { UserId = "cal-admin-1", Roles = new[] { RoleCodes.GlobalAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new CreateNotaryBlockHandler(
            new NotaryBlockRepository(db),
            new NotaryAppointmentRepository(db),
            scope);

        var command = new CreateNotaryBlockCommand
        {
            NotaryId = "cal-notary-target-1",
            Start = DateTime.UtcNow.AddDays(3),
            End = DateTime.UtcNow.AddDays(3).AddHours(1),
            Reason = "Admin-managed block"
        };

        var result = await handler.Handle(command, CancellationToken.None);
        result.Id.Should().NotBeEmpty();
    }

    // ------------------------------------------------------------ DeleteNotaryBlock

    [Fact]
    public async Task DeleteBlock_NotaryCaller_CannotDeleteAnotherNotarysBlock()
    {
        RequireDatabase();
        await SeedNotaryAsync("cal-notary-owner-3");
        await SeedNotaryAsync("cal-notary-intruder-3");
        var blockId = await SeedBlockAsync("cal-notary-owner-3");

        var intruder = new FakeCurrentUser { UserId = "cal-notary-intruder-3", Roles = new[] { RoleCodes.Notary } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, intruder);
        var handler = new DeleteNotaryBlockHandler(new NotaryBlockRepository(db), scope);

        var act = async () => await handler.Handle(
            new DeleteNotaryBlockCommand { NotaryId = "cal-notary-owner-3", BlockId = blockId },
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>(
            "a NOTARY must not be able to delete another notary's block");

        await using var verify = _fixture.CreateContext();
        (await verify.Set<NotaryBlock>().AnyAsync(b => b.Id == blockId)).Should().BeTrue(
            "the block must survive a denied cross-notary delete attempt");
    }

    // ------------------------------------------------------------ GetNotaryBlocks

    [Fact]
    public async Task GetBlocks_NotaryCaller_CannotReadAnotherNotarysBlocks()
    {
        RequireDatabase();
        await SeedNotaryAsync("cal-notary-owner-4");
        await SeedNotaryAsync("cal-notary-intruder-4");
        await SeedBlockAsync("cal-notary-owner-4");

        var intruder = new FakeCurrentUser { UserId = "cal-notary-intruder-4", Roles = new[] { RoleCodes.Notary } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, intruder);
        var handler = new GetNotaryBlocksHandler(new NotaryBlockRepository(db), scope);

        var act = async () => await handler.Handle(
            new GetNotaryBlocksQuery { NotaryId = "cal-notary-owner-4" },
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>(
            "a NOTARY must not be able to read another notary's blocks");
    }

    // ------------------------------------------------------------ SetNotaryWeeklyAvailability

    [Fact]
    public async Task SetWeeklyAvailability_NotaryCaller_CannotOverwriteAnotherNotarysSchedule()
    {
        RequireDatabase();
        await SeedNotaryAsync("cal-notary-owner-5");
        await SeedNotaryAsync("cal-notary-intruder-5");
        await SeedWeeklyAvailabilityAsync("cal-notary-owner-5");

        var intruder = new FakeCurrentUser { UserId = "cal-notary-intruder-5", Roles = new[] { RoleCodes.Notary } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, intruder);
        var handler = new SetNotaryWeeklyAvailabilityHandler(new WeeklyAvailabililtyRepository(db), scope);

        var command = new SetNotaryWeeklyAvailabilityCommand
        {
            NotaryId = "cal-notary-owner-5",
            Slots = new List<NotaryWeeklyAvailabilitySlot>
            {
                new() { DayOfWeek = DayOfWeek.Tuesday, StartTime = TimeSpan.FromHours(8), EndTime = TimeSpan.FromHours(12) }
            }
        };

        var act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>(
            "a NOTARY must not be able to replace another notary's weekly schedule");

        await using var verify = _fixture.CreateContext();
        var ownerSlots = await verify.Set<WeeklyAvailability>().Where(w => w.NotaryId == "cal-notary-owner-5").ToListAsync();
        ownerSlots.Should().ContainSingle(w => w.DayOfWeek == DayOfWeek.Monday,
            "the owner's original Monday slot must survive a denied cross-notary overwrite");
    }

    [Fact]
    public async Task SetWeeklyAvailability_NotaryCaller_CanReplaceOwnSchedule()
    {
        RequireDatabase();
        await SeedNotaryAsync("cal-notary-owner-6");
        await SeedWeeklyAvailabilityAsync("cal-notary-owner-6");

        var owner = new FakeCurrentUser { UserId = "cal-notary-owner-6", Roles = new[] { RoleCodes.Notary } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, owner);
        var handler = new SetNotaryWeeklyAvailabilityHandler(new WeeklyAvailabililtyRepository(db), scope);

        var command = new SetNotaryWeeklyAvailabilityCommand
        {
            NotaryId = "cal-notary-owner-6",
            Slots = new List<NotaryWeeklyAvailabilitySlot>
            {
                new() { DayOfWeek = DayOfWeek.Wednesday, StartTime = TimeSpan.FromHours(10), EndTime = TimeSpan.FromHours(14) }
            }
        };

        var result = await handler.Handle(command, CancellationToken.None);
        result.Should().ContainSingle(s => s.DayOfWeek == DayOfWeek.Wednesday);
    }

    // ------------------------------------------------------------ GetNotaryWeeklyAvailability

    [Fact]
    public async Task GetWeeklyAvailability_NotaryCaller_CannotReadAnotherNotarysSchedule()
    {
        RequireDatabase();
        await SeedNotaryAsync("cal-notary-owner-7");
        await SeedNotaryAsync("cal-notary-intruder-7");
        await SeedWeeklyAvailabilityAsync("cal-notary-owner-7");

        var intruder = new FakeCurrentUser { UserId = "cal-notary-intruder-7", Roles = new[] { RoleCodes.Notary } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, intruder);
        var handler = new GetNotaryWeeklyAvailabilityHandler(new WeeklyAvailabililtyRepository(db), scope);

        var act = async () => await handler.Handle(
            new GetNotaryWeeklyAvailabilityQuery { NotaryId = "cal-notary-owner-7" },
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>(
            "a NOTARY must not be able to read another notary's weekly availability");
    }

    [Fact]
    public async Task GetWeeklyAvailability_AdminCaller_CanReadAnyNotarysSchedule()
    {
        RequireDatabase();
        await SeedNotaryAsync("cal-notary-target-2");
        await SeedWeeklyAvailabilityAsync("cal-notary-target-2");

        var admin = new FakeCurrentUser { UserId = "cal-admin-2", Roles = new[] { RoleCodes.ProjectAdmin } };
        var db = _fixture.CreateContext();
        var scope = new ProjectScopeService(db, admin);
        var handler = new GetNotaryWeeklyAvailabilityHandler(new WeeklyAvailabililtyRepository(db), scope);

        var result = await handler.Handle(
            new GetNotaryWeeklyAvailabilityQuery { NotaryId = "cal-notary-target-2" },
            CancellationToken.None);

        result.Should().ContainSingle(s => s.DayOfWeek == DayOfWeek.Monday);
    }
}
