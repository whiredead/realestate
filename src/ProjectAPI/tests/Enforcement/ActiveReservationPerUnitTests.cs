using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Filters;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// Step 2 of work order #4: proves IX_Reservations_ActivePerUnit exists, actually
/// rejects a second active reservation, and that the resulting error reaches the
/// client as UNIT_NOT_AVAILABLE.
///
/// Before this work the index did not exist at all: three code comments and
/// ApiExceptionFilter named it as "the concurrency backstop" while the only real
/// guard was a read-then-write check that two simultaneous submits both pass.
/// </summary>
[Collection("sqlserver")]
public class ActiveReservationPerUnitTests
{
    private readonly SqlServerFixture _fixture;

    public ActiveReservationPerUnitTests(SqlServerFixture fixture) => _fixture = fixture;

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

    // ---------------------------------------------------------------- helpers

    private async Task<Guid> SeedUnitAsync()
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

        var unit = new Unit
        {
            Id = Guid.NewGuid(),
            ProjectId = immeuble.Id, // column named ProjectId actually holds the Immeuble id
            Floor = "1",
            UnitNumber = "A101",
            View = "Jardin",
            Orientation = "Nord",
            Status = UnitCommercialStatus.Available
        };

        db.Projects.Add(project);
        db.Immeubles.Add(immeuble);
        db.Units.Add(unit);
        await db.SaveChangesAsync();

        return unit.Id;
    }

    private static Reservation NewReservation(Guid unitId, ReservationStatus status) => new()
    {
        Id = Guid.NewGuid(),
        UnitId = unitId,
        Status = status,
        TotalPropertyPrice = 1_000_000m,
        ReservationAmount = 50_000m,
        ReservationDate = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        Name = "Test",
        LastName = "Buyer"
    };

    // ------------------------------------------------------------------ tests

    /// <summary>
    /// The heart of step 2: two submits racing for the same unit. Both pass the
    /// application-level availability check (neither can see the other's
    /// uncommitted row), so only the database can arbitrate.
    /// </summary>
    [Fact]
    public async Task Two_concurrent_submits_on_the_same_unit_leave_exactly_one_winner()
    {
        RequireDatabase();
        var unitId = await SeedUnitAsync();

        // Two independent contexts and connections — a genuine race, not two
        // writes through one change tracker.
        async Task<Exception?> Submit()
        {
            try
            {
                await using var db = _fixture.CreateContext();
                db.Reservations.Add(NewReservation(unitId, ReservationStatus.Pending));
                await db.SaveChangesAsync();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        var results = await Task.WhenAll(Submit(), Submit());

        var failures = results.Where(r => r is not null).ToList();
        var successes = results.Count(r => r is null);

        successes.Should().Be(1, "the filtered unique index must let exactly one submit through");
        failures.Should().HaveCount(1, "the loser of the race must be rejected by the database");

        await using var verify = _fixture.CreateContext();
        var active = await verify.Reservations
            .CountAsync(r => r.UnitId == unitId &&
                             (r.Status == ReservationStatus.Pending ||
                              r.Status == ReservationStatus.ChangesRequested ||
                              r.Status == ReservationStatus.Approved ||
                              r.Status == ReservationStatus.Sold));

        active.Should().Be(1, "a unit may carry at most one active reservation (§3, §6.3)");
    }

    /// <summary>
    /// The failure must reach the client as 409 UNIT_NOT_AVAILABLE, not a generic
    /// 500. ApiExceptionFilter recognises the violation by index name, so this
    /// feeds the REAL SqlException from the race through the REAL filter — which
    /// is the only way to show that branch is reachable at all.
    /// </summary>
    [Fact]
    public async Task Losing_the_race_is_translated_to_UNIT_NOT_AVAILABLE()
    {
        RequireDatabase();
        var unitId = await SeedUnitAsync();

        await using (var first = _fixture.CreateContext())
        {
            first.Reservations.Add(NewReservation(unitId, ReservationStatus.Pending));
            await first.SaveChangesAsync();
        }

        Exception? violation = null;
        try
        {
            await using var second = _fixture.CreateContext();
            second.Reservations.Add(NewReservation(unitId, ReservationStatus.Approved));
            await second.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            violation = ex;
        }

        violation.Should().NotBeNull("the second active reservation must be refused");

        // The exception must really be a unique-index violation naming our index.
        var sql = Unwrap(violation!);
        sql.Should().NotBeNull("the refusal must come from SQL Server, not from application code");
        new[] { 2601, 2627 }.Should().Contain(sql!.Number, "2601/2627 are the unique-violation numbers the filter looks for");
        sql.Message.Should().Contain("IX_Reservations_ActivePerUnit",
            "ApiExceptionFilter identifies this conflict by index name, so the name is part of the contract");

        // Now the part that proves reachability: run it through the filter.
        var context = NewExceptionContext(violation!);
        new ApiExceptionFilter(NullLogger<ApiExceptionFilter>.Instance).OnException(context);

        context.ExceptionHandled.Should().BeTrue();
        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status409Conflict);

        var problem = result.Value.Should().BeAssignableTo<ProblemDetails>().Subject;
        problem.Extensions["code"].Should().Be(BusinessErrorCodes.UnitNotAvailable);
    }

    /// <summary>
    /// A DRAFT must not block the unit (§5.3), so the index filter must exclude
    /// it. Two drafts on one unit are legitimate.
    /// </summary>
    [Fact]
    public async Task Drafts_do_not_block_the_unit()
    {
        RequireDatabase();
        var unitId = await SeedUnitAsync();

        await using var db = _fixture.CreateContext();
        db.Reservations.Add(NewReservation(unitId, ReservationStatus.Draft));
        db.Reservations.Add(NewReservation(unitId, ReservationStatus.Draft));

        var save = async () => await db.SaveChangesAsync();
        await save.Should().NotThrowAsync("DRAFT is deliberately outside the blocking set (§5.3)");
    }

    /// <summary>
    /// Once the active reservation reaches a terminal state the unit frees up, so
    /// a new reservation must be accepted — otherwise a rejected file would strand
    /// its unit forever.
    /// </summary>
    [Fact]
    public async Task A_terminal_reservation_releases_the_unit_for_a_new_one()
    {
        RequireDatabase();
        var unitId = await SeedUnitAsync();

        await using var db = _fixture.CreateContext();

        var first = NewReservation(unitId, ReservationStatus.Pending);
        db.Reservations.Add(first);
        await db.SaveChangesAsync();

        first.Status = ReservationStatus.Rejected;
        await db.SaveChangesAsync();

        db.Reservations.Add(NewReservation(unitId, ReservationStatus.Pending));

        var save = async () => await db.SaveChangesAsync();
        await save.Should().NotThrowAsync("a rejected reservation no longer blocks its unit");
    }

    /// <summary>
    /// Guards the comment in ReservationStateMachine: the blocking set and the
    /// index filter (0, 1, 4, 6) must not drift apart. If someone adds a status to
    /// the set without changing the migration, this fails.
    /// </summary>
    [Fact]
    public void Blocking_status_set_matches_the_index_filter()
    {
        ReservationStateMachine.UnitBlockingStatuses
            .Select(s => (int)s)
            .OrderBy(i => i)
            .Should().Equal(new[] { 0, 1, 4, 6 },
                "IX_Reservations_ActivePerUnit filters on [Status] IN (0, 1, 4, 6)");
    }

    // ---------------------------------------------------------------- plumbing

    private static SqlException? Unwrap(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql) return sql;
        }
        return null;
    }

    private static ExceptionContext NewExceptionContext(Exception exception)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/Reservations/create";

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ExceptionContext(actionContext, new List<IFilterMetadata>()) { Exception = exception };
    }
}
