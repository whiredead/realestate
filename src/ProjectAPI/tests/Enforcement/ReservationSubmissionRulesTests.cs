using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Reservations;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Reservations.Entities;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// Scope E — the two rules that decide whether a reservation may be opened and
/// submitted: the project's commercial phase, and its document requirements.
///
/// The document half is tested against a real database because the property
/// that matters is a negative one: a project with NO requirement rows must
/// behave exactly as it did before the feature existed. That is a statement
/// about what an empty table does, and an empty table is only convincing when
/// it is a real one.
/// </summary>
[Collection("sqlserver")]
public class ReservationSubmissionRulesTests
{
    private readonly SqlServerFixture _fixture;

    public ReservationSubmissionRulesTests(SqlServerFixture fixture) => _fixture = fixture;

    private void RequireDatabase()
    {
        if (!_fixture.Available)
        {
            throw new InvalidOperationException(
                "These tests assert real submission rules and cannot run without SQL Server. " +
                $"Connection failed: {_fixture.UnavailableReason}");
        }
    }

    /// <summary>Seeds project → immeuble → floor → unit → reservation and returns the ids.</summary>
    private async Task<(Guid ProjectId, Guid UnitId, Guid ReservationId)> SeedAsync(
        string label, string projectStatus = ProjectStatusCodes.SurPlan)
    {
        await using var db = _fixture.CreateContext();

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = $"DocReq-{label}",
            Location = "Casablanca",
            Address = "1 rue de test",
            Description = "seed",
            Module3DLink = string.Empty,
            StatusGlobal = projectStatus
        };

        var immeuble = new Immeuble
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = $"Immeuble-{label}",
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
            ProjectId = immeuble.Id, // column named ProjectId actually holds the Immeuble id
            FloorId = floor.Id,
            UnitNumber = $"U-{label}",
            View = "Jardin",
            Orientation = "Nord",
            Status = UnitCommercialStatus.Available
        };

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            UnitId = unit.Id,
            Status = ReservationStatus.Draft,
            TotalPropertyPrice = 1_000_000m,
            ReservationAmount = 50_000m,
            ReservationDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Name = "Test",
            LastName = "Buyer"
        };

        db.Add(project);
        db.Add(immeuble);
        db.Add(floor);
        db.Add(unit);
        db.Add(reservation);
        await db.SaveChangesAsync();

        return (project.Id, unit.Id, reservation.Id);
    }

    // --------------------------------------------------- §12.1 document rules

    [Fact]
    public async Task A_project_with_no_requirements_requires_nothing()
    {
        RequireDatabase();

        // THE regression guard for this feature: turning it on must not make a
        // single existing project start refusing submissions.
        var (_, unitId, reservationId) = await SeedAsync("none");

        await using var db = _fixture.CreateContext();
        var checklist = new ReservationDocumentChecklist(db);

        var act = async () => await checklist.EnsureSubmittableAsync(reservationId, unitId, CancellationToken.None);

        await act.Should().NotThrowAsync();
        (await checklist.BuildAsync(reservationId, unitId, CancellationToken.None))
            .Should().BeEmpty("no requirement rows means no checklist at all");
    }

    [Fact]
    public async Task A_required_document_that_is_missing_blocks_submission()
    {
        RequireDatabase();

        var (projectId, unitId, reservationId) = await SeedAsync("missing");

        await using (var db = _fixture.CreateContext())
        {
            db.Add(new ProjectDocumentRequirement
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                DocumentType = "CIN",
                LabelFr = "Copie de la CIN",
                IsRequired = true,
                SequenceNo = 1
            });
            await db.SaveChangesAsync();
        }

        await using var check = _fixture.CreateContext();
        var checklist = new ReservationDocumentChecklist(check);

        var act = async () => await checklist.EnsureSubmittableAsync(reservationId, unitId, CancellationToken.None);

        (await act.Should().ThrowAsync<BusinessRuleException>())
            .Which.Code.Should().Be(BusinessErrorCodes.MissingRequiredDocument);
    }

    [Fact]
    public async Task Providing_the_document_unblocks_submission_and_matching_ignores_case()
    {
        RequireDatabase();

        var (projectId, unitId, reservationId) = await SeedAsync("provided");

        await using (var db = _fixture.CreateContext())
        {
            db.Add(new ProjectDocumentRequirement
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                DocumentType = "CIN",
                LabelFr = "Copie de la CIN",
                IsRequired = true,
                SequenceNo = 1
            });

            db.Add(new ReservationDocument
            {
                Id = Guid.NewGuid(),
                ReservationId = reservationId,
                FileName = "cin.pdf",
                Url = "https://storage.test/cin.pdf",
                // Lower case on purpose: the requirement is stored upper-cased,
                // and an agent's upload must not fail to count because of it.
                DocumentType = "cin"
            });

            await db.SaveChangesAsync();
        }

        await using var check = _fixture.CreateContext();
        var checklist = new ReservationDocumentChecklist(check);

        await checklist.Invoking(c => c.EnsureSubmittableAsync(reservationId, unitId, CancellationToken.None))
            .Should().NotThrowAsync();

        var items = await checklist.BuildAsync(reservationId, unitId, CancellationToken.None);
        items.Should().ContainSingle().Which.IsProvided.Should().BeTrue();
    }

    [Fact]
    public async Task An_optional_requirement_appears_on_the_checklist_but_never_blocks()
    {
        RequireDatabase();

        var (projectId, unitId, reservationId) = await SeedAsync("optional");

        await using (var db = _fixture.CreateContext())
        {
            db.Add(new ProjectDocumentRequirement
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                DocumentType = "RIB",
                LabelFr = "RIB",
                IsRequired = false,
                SequenceNo = 1
            });
            await db.SaveChangesAsync();
        }

        await using var check = _fixture.CreateContext();
        var checklist = new ReservationDocumentChecklist(check);

        // An optional requirement is a checklist item, not an absent one — the
        // agent is still prompted for it.
        var items = await checklist.BuildAsync(reservationId, unitId, CancellationToken.None);
        items.Should().ContainSingle().Which.IsRequired.Should().BeFalse();

        await checklist.Invoking(c => c.EnsureSubmittableAsync(reservationId, unitId, CancellationToken.None))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task One_requirement_per_document_type_per_project_is_enforced_by_the_database()
    {
        RequireDatabase();

        // Two rows for "CIN" would let a project both require and not require
        // the same document, and the checklist would show it twice.
        var (projectId, _, _) = await SeedAsync("unique");

        await using var db = _fixture.CreateContext();
        db.Add(new ProjectDocumentRequirement
        {
            Id = Guid.NewGuid(), ProjectId = projectId, DocumentType = "CIN", LabelFr = "CIN", SequenceNo = 1
        });
        db.Add(new ProjectDocumentRequirement
        {
            Id = Guid.NewGuid(), ProjectId = projectId, DocumentType = "CIN", LabelFr = "CIN (bis)", SequenceNo = 2
        });

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // ------------------------------------------------------ §5.2 phase gate

    [Theory]
    [InlineData(ProjectStatusCodes.SurPlan, true)]
    [InlineData(ProjectStatusCodes.EnLivraison, true)]
    [InlineData(ProjectStatusCodes.Finalise, false)]
    [InlineData("IN_PROGRESS", true)]
    [InlineData("COMPLETED", true)]
    [InlineData("ARCHIVED", false)]
    public void New_reservations_are_allowed_until_the_project_is_finalised(string status, bool allowed)
    {
        // A reservation opens a commercial file on a project still on the market: sold off-plan (SUR_PLAN)
        // or in delivery (EN_LIVRAISON). FINALISE accepts nothing.
        ProjectStatusCodes.AllowsNewReservation(status).Should().Be(allowed);
    }
}
