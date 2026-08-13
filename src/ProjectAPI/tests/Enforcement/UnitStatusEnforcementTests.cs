using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectAPI.Api.Application.Common.Units;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// Step 3 of work order #4: the unit commercial state machine.
///
/// Before this work, <c>unit.Status</c> was written in exactly one place — the
/// generic UpdateUnit CRUD handler — so approving a reservation left the unit
/// untouched and a completed sale never marked it SOLD. These tests assert the
/// matrix is enforced, that history is written, and that the database itself
/// refuses a status outside the §3 vocabulary.
/// </summary>
public class UnitStateMachineTests
{
    [Theory]
    // The §3 happy path, step by step.
    [InlineData(UnitCommercialStatus.Available, UnitCommercialStatus.HoldPendingApproval)]
    [InlineData(UnitCommercialStatus.HoldPendingApproval, UnitCommercialStatus.Reserved)]
    [InlineData(UnitCommercialStatus.HoldPendingApproval, UnitCommercialStatus.Available)]
    [InlineData(UnitCommercialStatus.Reserved, UnitCommercialStatus.Sold)]
    [InlineData(UnitCommercialStatus.Reserved, UnitCommercialStatus.Contracted)]
    [InlineData(UnitCommercialStatus.Contracted, UnitCommercialStatus.Sold)]
    [InlineData(UnitCommercialStatus.Sold, UnitCommercialStatus.Delivered)]
    // Administrative moves.
    [InlineData(UnitCommercialStatus.Reserved, UnitCommercialStatus.Available)]
    [InlineData(UnitCommercialStatus.Available, UnitCommercialStatus.Suspended)]
    [InlineData(UnitCommercialStatus.Suspended, UnitCommercialStatus.Available)]
    public void Allows_the_transitions_the_spec_defines(UnitCommercialStatus from, UnitCommercialStatus to) =>
        UnitStateMachine.CanTransition(from, to).Should().BeTrue();

    [Theory]
    // Cannot skip the approval hold.
    [InlineData(UnitCommercialStatus.Available, UnitCommercialStatus.Reserved)]
    [InlineData(UnitCommercialStatus.Available, UnitCommercialStatus.Sold)]
    // Cannot sell a unit that was never reserved.
    [InlineData(UnitCommercialStatus.HoldPendingApproval, UnitCommercialStatus.Sold)]
    // Cannot deliver before the notary completes the purchase.
    [InlineData(UnitCommercialStatus.Reserved, UnitCommercialStatus.Delivered)]
    // Terminal states are terminal.
    [InlineData(UnitCommercialStatus.Delivered, UnitCommercialStatus.Available)]
    [InlineData(UnitCommercialStatus.Delivered, UnitCommercialStatus.Suspended)]
    [InlineData(UnitCommercialStatus.Cancelled, UnitCommercialStatus.Available)]
    public void Refuses_transitions_outside_the_matrix(UnitCommercialStatus from, UnitCommercialStatus to)
    {
        UnitStateMachine.CanTransition(from, to).Should().BeFalse();

        var act = () => UnitStateMachine.EnsureCanTransition(from, to);
        act.Should().Throw<InvalidUnitTransitionException>();
    }

    [Fact]
    public void Only_AVAILABLE_units_are_selectable()
    {
        UnitStateMachine.IsSelectable(UnitCommercialStatus.Available).Should().BeTrue();

        foreach (var status in Enum.GetValues<UnitCommercialStatus>().Where(s => s != UnitCommercialStatus.Available))
        {
            UnitStateMachine.IsSelectable(status).Should().BeFalse($"{status} must not be reservable (§3)");
        }
    }

    [Fact]
    public void Delivered_and_cancelled_are_terminal()
    {
        UnitStateMachine.IsTerminal(UnitCommercialStatus.Delivered).Should().BeTrue();
        UnitStateMachine.IsTerminal(UnitCommercialStatus.Cancelled).Should().BeTrue();
        UnitStateMachine.IsTerminal(UnitCommercialStatus.Reserved).Should().BeFalse();
    }

    [Fact]
    public void Every_status_round_trips_through_its_canonical_code()
    {
        foreach (var status in Enum.GetValues<UnitCommercialStatus>())
        {
            UnitStatusCodes.Parse(status.ToCode()).Should().Be(status);
        }

        // The CHECK constraint is generated from this list, so it must be complete.
        UnitStatusCodes.All.Should().HaveCount(Enum.GetValues<UnitCommercialStatus>().Length);
    }

    [Fact]
    public void Legacy_spellings_still_parse()
    {
        UnitStatusCodes.Parse("Available").Should().Be(UnitCommercialStatus.Available);
        UnitStatusCodes.Parse("Reserved").Should().Be(UnitCommercialStatus.Reserved);
        UnitStatusCodes.Parse("Sold").Should().Be(UnitCommercialStatus.Sold);
    }
}

/// <summary>
/// The service is the single writer. These tests use a real database so the
/// history row and the CHECK constraint are exercised, not mocked.
/// </summary>
[Collection("sqlserver")]
public class UnitStatusServiceTests
{
    private readonly SqlServerFixture _fixture;

    public UnitStatusServiceTests(SqlServerFixture fixture) => _fixture = fixture;

    /// <summary>
    /// xunit 2.x has no first-class Skip API, so an unreachable database throws a
    /// clear failure instead of silently passing. A green run therefore always
    /// means the constraints were really exercised.
    /// </summary>
    private void RequireDatabase()
    {
        if (!_fixture.Available)
        {
            throw new InvalidOperationException(
                "These tests assert real database constraints and cannot run without SQL Server. " +
                $"Connection failed: {_fixture.UnavailableReason}");
        }
    }

    private async Task<Guid> SeedUnitAsync(UnitCommercialStatus status = UnitCommercialStatus.Available)
    {
        await using var db = _fixture.CreateContext();

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Test project",
            Location = "Casablanca",
            Address = "1 rue de test",
            Description = "seed",
            Module3DLink = string.Empty,
            StatusGlobal = "DRAFT"
        };

        var immeuble = new Immeuble
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = "Immeuble A",
            Location = "Casablanca",
            Type = "Appartements",
            Description = "seed",
            Images = null,
            MinSellableSurfaceRange = 50,
            MaxSellableSurfaceRange = 100,
            Status = "IN_PROGRESS"
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
            UnitNumber = "A101",
            View = "Jardin",
            Orientation = "Nord",
            Status = status
        };

        db.Projects.Add(project);
        db.Immeubles.Add(immeuble);
        db.Set<Floor>().Add(floor);
        db.Units.Add(unit);
        await db.SaveChangesAsync();

        return unit.Id;
    }

    private static UnitStatusService NewService(Infrastructure.Context.ApplicationDbContext db) =>
        new(db, NullLogger<UnitStatusService>.Instance);

    [Fact]
    public async Task A_valid_transition_persists_the_status_and_writes_history()
    {
        RequireDatabase();
        var unitId = await SeedUnitAsync();
        var reservationId = Guid.NewGuid();

        await using (var db = _fixture.CreateContext())
        {
            await NewService(db).TransitionAsync(
                unitId,
                UnitCommercialStatus.HoldPendingApproval,
                UnitStatusCause.ReservationSubmitted,
                reservationId: reservationId,
                actorUserId: "agent-1");

            await db.SaveChangesAsync();
        }

        await using var verify = _fixture.CreateContext();

        var unit = await verify.Units.SingleAsync(u => u.Id == unitId);
        unit.Status.Should().Be(UnitCommercialStatus.HoldPendingApproval);

        var history = await verify.UnitStatusHistories.Where(h => h.UnitId == unitId).ToListAsync();
        history.Should().ContainSingle("every transition writes exactly one history row (§7)");
        history[0].FromStatus.Should().Be(UnitCommercialStatus.Available);
        history[0].ToStatus.Should().Be(UnitCommercialStatus.HoldPendingApproval);
        history[0].Cause.Should().Be(UnitStatusCause.ReservationSubmitted);
        history[0].ReservationId.Should().Be(reservationId);
        history[0].ActorUserId.Should().Be("agent-1");
    }

    [Fact]
    public async Task An_invalid_transition_is_refused_and_changes_nothing()
    {
        RequireDatabase();
        var unitId = await SeedUnitAsync();

        await using (var db = _fixture.CreateContext())
        {
            // AVAILABLE → SOLD skips the whole workflow.
            var act = async () => await NewService(db).TransitionAsync(
                unitId, UnitCommercialStatus.Sold, UnitStatusCause.NotaryPurchaseCompleted);

            await act.Should().ThrowAsync<InvalidUnitTransitionException>();
        }

        await using var verify = _fixture.CreateContext();
        (await verify.Units.SingleAsync(u => u.Id == unitId)).Status
            .Should().Be(UnitCommercialStatus.Available, "a refused transition must leave the unit untouched");
        (await verify.UnitStatusHistories.CountAsync(h => h.UnitId == unitId))
            .Should().Be(0, "a refused transition must not write history");
    }

    [Fact]
    public async Task The_idempotent_variant_writes_no_history_when_nothing_moves()
    {
        RequireDatabase();
        var unitId = await SeedUnitAsync(UnitCommercialStatus.HoldPendingApproval);

        await using (var db = _fixture.CreateContext())
        {
            await NewService(db).TransitionIfNeededAsync(
                unitId, UnitCommercialStatus.HoldPendingApproval, UnitStatusCause.ReservationSubmitted);
            await db.SaveChangesAsync();
        }

        await using var verify = _fixture.CreateContext();
        (await verify.UnitStatusHistories.CountAsync(h => h.UnitId == unitId))
            .Should().Be(0, "re-asserting an existing hold is not a state change (§5.3)");
    }

    /// <summary>
    /// The last line of defence: even code that bypasses the service entirely
    /// cannot store a status outside the §3 vocabulary.
    /// </summary>
    [Fact]
    public async Task The_database_rejects_a_status_outside_the_spec_vocabulary()
    {
        RequireDatabase();
        var unitId = await SeedUnitAsync();

        await using var db = _fixture.CreateContext();

        var act = async () => await db.Database.ExecuteSqlRawAsync(
            "UPDATE [Units] SET [Status] = 'NotAStatus' WHERE [Id] = {0}", unitId);

        (await act.Should().ThrowAsync<Exception>("CK_Units_Status must reject unknown codes"))
            .Which.Message.Should().Contain("CK_Units_Status");
    }

    /// <summary>
    /// The whole §3 happy path, end to end, in the order the sale workflow drives
    /// it — submit, approve, notary, handover — each step leaving a history row.
    /// </summary>
    [Fact]
    public async Task The_full_sale_path_walks_AVAILABLE_to_DELIVERED()
    {
        RequireDatabase();
        var unitId = await SeedUnitAsync();
        var reservationId = Guid.NewGuid();

        await using (var db = _fixture.CreateContext())
        {
            var service = NewService(db);

            await service.TransitionAsync(unitId, UnitCommercialStatus.HoldPendingApproval, UnitStatusCause.ReservationSubmitted, reservationId);
            await db.SaveChangesAsync();

            await service.TransitionAsync(unitId, UnitCommercialStatus.Reserved, UnitStatusCause.ReservationApproved, reservationId);
            await db.SaveChangesAsync();

            await service.TransitionAsync(unitId, UnitCommercialStatus.Sold, UnitStatusCause.NotaryPurchaseCompleted, reservationId);
            await db.SaveChangesAsync();

            await service.TransitionAsync(unitId, UnitCommercialStatus.Delivered, UnitStatusCause.HandoverAcknowledged, reservationId);
            await db.SaveChangesAsync();
        }

        await using var verify = _fixture.CreateContext();

        (await verify.Units.SingleAsync(u => u.Id == unitId)).Status
            .Should().Be(UnitCommercialStatus.Delivered);

        var trail = await verify.UnitStatusHistories
            .Where(h => h.UnitId == unitId)
            .OrderBy(h => h.OccurredAt)
            .Select(h => h.ToStatus)
            .ToListAsync();

        trail.Should().Equal(
            UnitCommercialStatus.HoldPendingApproval,
            UnitCommercialStatus.Reserved,
            UnitCommercialStatus.Sold,
            UnitCommercialStatus.Delivered);
    }
}
