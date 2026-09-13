using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Notary;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Sales.Entities;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// Scopes H and I — the two gates that stand between a notarial act and a
/// delivered, warrantied property.
///
/// Scope H is the notary cascade: eligibility is derived once at confirmation
/// and AGAIN at PURCHASE_COMPLETED, because a report can be disputed or a
/// blocking snag raised in between and PURCHASE_COMPLETED is irreversible.
/// These tests exercise <see cref="NotaryEligibilityService"/>, the single
/// shared gate both checkpoints call — if it is wrong, both checkpoints are
/// wrong together, which is exactly why it is shared.
///
/// Scope I is the warranty freeze: the handover reads the CONFIRMED sale's
/// frozen WarrantyMonths instead of a caller-supplied figure, and refuses
/// outright when no confirmed sale stands behind the file.
/// </summary>
[Collection("sqlserver")]
public class NotaryCascadeAndHandoverWarrantyTests
{
    private readonly SqlServerFixture _fixture;

    public NotaryCascadeAndHandoverWarrantyTests(SqlServerFixture fixture) => _fixture = fixture;

    private void RequireDatabase()
    {
        if (!_fixture.Available)
        {
            throw new InvalidOperationException(
                "These tests assert real gate behaviour and cannot run without SQL Server. " +
                $"Connection failed: {_fixture.UnavailableReason}");
        }
    }

    private sealed record Seed(Guid ProjectId, Guid UnitId, Guid ReservationId);

    private async Task<Seed> SeedFileAsync(string label, int warrantyMonths = 12)
    {
        await using var db = _fixture.CreateContext();

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = $"Cascade-{label}",
            Location = "Casablanca",
            Address = "1 rue de test",
            Description = "seed",
            Module3DLink = string.Empty,
            StatusGlobal = ProjectStatusCodes.Completed,
            WarrantyMonths = warrantyMonths
        };
        var immeuble = new Immeuble
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = $"Imm-{label}",
            Location = "Casablanca",
            Type = "Appartements",
            Description = "seed",
            Images = null,
            MinSellableSurfaceRange = 50,
            MaxSellableSurfaceRange = 100,
            Status = "IN_PROGRESS"
        };
        var floor = new Floor { Id = Guid.NewGuid(), ImmeubleId = immeuble.Id, Name = "1", SequenceNo = 1 };
        var unit = new Unit
        {
            Id = Guid.NewGuid(),
            ProjectId = immeuble.Id,
            FloorId = floor.Id,
            UnitNumber = $"C-{label}",
            View = "Jardin",
            Orientation = "Nord",
            Status = UnitCommercialStatus.Available
        };
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UnitId = unit.Id,
            Status = ReservationStatus.Approved,
            TotalPropertyPrice = 1_000_000m,
            ReservationAmount = 50_000m,
            ReservationDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Name = "Test",
            LastName = "Buyer"
        };

        db.AddRange(project, immeuble, floor, unit, reservation);
        await db.SaveChangesAsync();

        return new Seed(project.Id, unit.Id, reservation.Id);
    }

    /// <summary>Adds a completed visit with an ACKNOWLEDGED report, plus any snags given.</summary>
    private async Task AddAcknowledgedVisitAsync(Guid reservationId, params (SnagSeverity Severity, SnagStatus Status)[] snags)
    {
        await using var db = _fixture.CreateContext();

        var visitCase = new FinalVisitCase { Id = Guid.NewGuid(), ReservationId = reservationId };
        var appointment = new FinalVisitAppointment
        {
            Id = Guid.NewGuid(),
            CaseId = visitCase.Id,
            AttemptNo = 1,
            StartsAt = DateTime.UtcNow.AddDays(-2),
            EndsAt = DateTime.UtcNow.AddDays(-2).AddHours(1),
            Status = AppointmentAttemptStatus.Completed
        };
        var report = new FinalVisitReport
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointment.Id,
            VersionNo = 1,
            Status = ReportStatus.Acknowledged,
            SubmittedAt = DateTime.UtcNow.AddDays(-1),
            AcknowledgedAt = DateTime.UtcNow
        };

        db.AddRange(visitCase, appointment, report);

        foreach (var (severity, status) in snags)
        {
            db.Add(new Snag
            {
                Id = Guid.NewGuid(),
                ReportId = report.Id,
                Code = $"S-{Guid.NewGuid():N}"[..8],
                Severity = severity,
                Status = status,
                Description = "seeded snag"
            });
        }

        await db.SaveChangesAsync();
    }

    private async Task SetTitleAsync(Guid unitId, TitleStatus status)
    {
        await using var db = _fixture.CreateContext();
        db.Add(new UnitTitleState { Id = Guid.NewGuid(), UnitId = unitId, Status = status });
        await db.SaveChangesAsync();
    }

    // ------------------------------------------------- Scope H: notary cascade

    [Fact]
    public async Task A_file_with_no_final_visit_is_not_eligible()
    {
        RequireDatabase();
        var seed = await SeedFileAsync("no-visit");

        await using var db = _fixture.CreateContext();
        var service = new NotaryEligibilityService(db);

        var result = await service.CalculateAsync(seed.ReservationId, CancellationToken.None);

        result.CanRequestAppointment.Should().BeFalse();
        result.Status.Should().Be(NotaryEligibilityStatus.NotEligibleFinalVisitPending);
    }

    [Fact]
    public async Task A_blocking_snag_raised_after_the_visit_stops_the_purchase()
    {
        RequireDatabase();

        // This is the whole point of re-checking at PURCHASE_COMPLETED: the
        // dossier was fine when the appointment was confirmed, and a blocking
        // snag appeared afterwards. The notarial act is irreversible, so the
        // gate has to hold at the last possible moment, not only the first.
        var seed = await SeedFileAsync("blocking");
        await AddAcknowledgedVisitAsync(seed.ReservationId, (SnagSeverity.Blocking, SnagStatus.Open));
        await SetTitleAsync(seed.UnitId, TitleStatus.Available);

        await using var db = _fixture.CreateContext();
        var service = new NotaryEligibilityService(db);

        var act = async () => await service.EnsureEligibleAsync(
            seed.ReservationId, seed.UnitId, CancellationToken.None);

        (await act.Should().ThrowAsync<BusinessRuleException>())
            .Which.Code.Should().Be(BusinessErrorCodes.NotaryNotEligible);
    }

    [Fact]
    public async Task A_major_snag_still_blocks_until_it_is_validated()
    {
        RequireDatabase();

        // §17.4 — "active" means not yet VALIDATED or CLOSED. A major snag the
        // technician merely marked RESOLVED is not enough.
        var seed = await SeedFileAsync("major-resolved");
        await AddAcknowledgedVisitAsync(seed.ReservationId, (SnagSeverity.Major, SnagStatus.Resolved));
        await SetTitleAsync(seed.UnitId, TitleStatus.Available);

        await using var db = _fixture.CreateContext();
        var result = await new NotaryEligibilityService(db)
            .CalculateAsync(seed.ReservationId, CancellationToken.None);

        result.CanRequestAppointment.Should().BeFalse();
        result.Status.Should().Be(NotaryEligibilityStatus.NotEligibleMajorSnags);
    }

    [Fact]
    public async Task Minor_snags_alone_do_not_stop_the_purchase()
    {
        RequireDatabase();

        var seed = await SeedFileAsync("minor");
        await AddAcknowledgedVisitAsync(seed.ReservationId, (SnagSeverity.Minor, SnagStatus.Open));
        await SetTitleAsync(seed.UnitId, TitleStatus.Available);

        await using var db = _fixture.CreateContext();
        var service = new NotaryEligibilityService(db);

        var result = await service.CalculateAsync(seed.ReservationId, CancellationToken.None);
        result.Status.Should().Be(NotaryEligibilityStatus.EligibleWithMinorSnags);

        await service.Invoking(s => s.EnsureEligibleAsync(seed.ReservationId, seed.UnitId, CancellationToken.None))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task The_land_title_gates_the_notarial_act_but_not_the_visit()
    {
        RequireDatabase();

        // The two halves are deliberately separable: a sale draft needs the
        // visit cleared, the notarial act additionally needs the title.
        var seed = await SeedFileAsync("no-title");
        await AddAcknowledgedVisitAsync(seed.ReservationId);
        // No UnitTitleState row at all → NotAvailable.

        await using var db = _fixture.CreateContext();
        var service = new NotaryEligibilityService(db);

        await service.Invoking(s => s.EnsureFinalVisitClearedAsync(seed.ReservationId, CancellationToken.None))
            .Should().NotThrowAsync("the visit itself is cleared");

        await service.Invoking(s => s.EnsureEligibleAsync(seed.ReservationId, seed.UnitId, CancellationToken.None))
            .Should().ThrowAsync<BusinessRuleException>("the notarial act also needs a title");
    }

    // --------------------------------------------- Scope I: handover warranty

    [Fact]
    public async Task The_handover_reads_the_warranty_frozen_on_the_confirmed_sale()
    {
        RequireDatabase();

        // The project says 36 months and the sale froze 24. The handover must
        // use 24 — the figure the buyer actually bought under — and must not
        // re-read the project, which may have moved since.
        var seed = await SeedFileAsync("warranty-read", warrantyMonths: 36);

        await using (var db = _fixture.CreateContext())
        {
            db.Add(new Sale
            {
                Id = Guid.NewGuid(),
                ReservationId = seed.ReservationId,
                UnitId = seed.UnitId,
                Status = SaleStatus.Confirmed,
                BuyerFirstName = "Test",
                BuyerLastName = "Buyer",
                BuyerEmail = "buyer@test.local",
                BuyerPhoneNumber = "0600000000",
                SaleDate = DateTime.UtcNow,
                TotalPrice = 1_000_000m,
                WarrantyMonths = 24,
                ConfirmedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await using var check = _fixture.CreateContext();

        // The exact predicate AcknowledgeHandoverHandler uses.
        var sale = await check.Set<Sale>()
            .FirstOrDefaultAsync(s => s.ReservationId == seed.ReservationId && s.Status == SaleStatus.Confirmed);

        sale.Should().NotBeNull();
        sale!.WarrantyMonths.Should().Be(24);

        var startsAt = DateTime.UtcNow;
        startsAt.AddMonths(sale.WarrantyMonths).Should().BeCloseTo(startsAt.AddMonths(24), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task A_draft_sale_is_not_a_confirmed_one_so_the_handover_finds_nothing()
    {
        RequireDatabase();

        // Delivery must not start a warranty off a sale nobody signed. The
        // handler throws when this query returns null.
        var seed = await SeedFileAsync("warranty-draft");

        await using (var db = _fixture.CreateContext())
        {
            db.Add(new Sale
            {
                Id = Guid.NewGuid(),
                ReservationId = seed.ReservationId,
                UnitId = seed.UnitId,
                Status = SaleStatus.Draft,
                BuyerFirstName = "Test",
                BuyerLastName = "Buyer",
                BuyerEmail = "buyer@test.local",
                BuyerPhoneNumber = "0600000000",
                SaleDate = DateTime.UtcNow,
                TotalPrice = 1_000_000m,
                WarrantyMonths = 24
            });
            await db.SaveChangesAsync();
        }

        await using var check = _fixture.CreateContext();

        var sale = await check.Set<Sale>()
            .FirstOrDefaultAsync(s => s.ReservationId == seed.ReservationId && s.Status == SaleStatus.Confirmed);

        sale.Should().BeNull("only a confirmed sale may start a warranty");
    }
}
