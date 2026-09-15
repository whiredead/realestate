using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.PublicCatalogue;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// §7.2 — a plan must list the stock that actually realises it.
///
/// The catalogue used to infer a unit's layout from its bedroom count alone,
/// which cannot separate two types that share one: a 3-bedroom apartment from a
/// 3-bedroom penthouse, or a studio from an office plateau at zero bedrooms.
/// Unit.TypeBienId makes it exact, with the bedroom match kept as a fallback for
/// stock created before the column existed.
///
/// These tests seed both shapes and read the real GetPublicPlansQuery, so they
/// fail if the matching rule regresses to bedroom counts.
/// </summary>
[Collection("sqlserver")]
public class UnitTypeBienMatchingTests
{
    private readonly SqlServerFixture _fixture;

    public UnitTypeBienMatchingTests(SqlServerFixture fixture) => _fixture = fixture;

    private void RequireDatabase()
    {
        if (!_fixture.Available)
        {
            throw new InvalidOperationException(
                "These tests assert the real plan/stock matching and cannot run without SQL Server. " +
                $"Connection failed: {_fixture.UnavailableReason}");
        }
    }

    /// <summary>
    /// One project offering two layouts that share a bedroom count — exactly the
    /// case bedroom matching cannot resolve — plus one building and one floor.
    /// </summary>
    private async Task<(Guid ProjectId, int ApartmentTypeId, int PenthouseTypeId, Guid FloorId, Guid ImmeubleId)>
        SeedProjectWithTwoThreeBedroomTypesAsync()
    {
        await using var db = _fixture.CreateContext();

        var apartment = new TypeBien { Name = $"Appartement 3 Ch {Guid.NewGuid():N}", NbrChambre = 3 };
        var penthouse = new TypeBien { Name = $"Penthouse {Guid.NewGuid():N}", NbrChambre = 3 };
        db.Set<TypeBien>().AddRange(apartment, penthouse);
        await db.SaveChangesAsync();

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = $"Matching {Guid.NewGuid():N}",
            Location = "Casablanca",
            Address = "Boulevard des Almohades",
            Description = "Seeded by UnitTypeBienMatchingTests.",
            Module3DLink = string.Empty,
            StatusGlobal = ProjectStatusCodes.SurPlan
        };
        db.Set<Project>().Add(project);

        db.Set<ProjectTypeBien>().AddRange(
            new ProjectTypeBien { Id = Guid.NewGuid(), ProjectId = project.Id, TypeBienId = apartment.Id },
            new ProjectTypeBien { Id = Guid.NewGuid(), ProjectId = project.Id, TypeBienId = penthouse.Id });

        var immeuble = new Immeuble
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = "Bâtiment A",
            Status = ProjectStatusCodes.SurPlan,
            MinPrice = 0,
            MaxPrice = 0
        };
        db.Set<Immeuble>().Add(immeuble);

        var floor = new Floor { Id = Guid.NewGuid(), ImmeubleId = immeuble.Id, Name = "1er étage", SequenceNo = 1 };
        db.Set<Floor>().Add(floor);

        await db.SaveChangesAsync();
        return (project.Id, apartment.Id, penthouse.Id, floor.Id, immeuble.Id);
    }

    private static Unit NewUnit(Guid immeubleId, Guid floorId, string number, int bedrooms, int? typeBienId, decimal price) =>
        new()
        {
            Id = Guid.NewGuid(),
            ProjectId = immeubleId, // legacy naming: this FK points at the Immeuble
            FloorId = floorId,
            UnitNumber = number,
            NumberOfBedrooms = bedrooms,
            NumberOfBathrooms = 2,
            View = "Mer",
            Orientation = "Ouest",
            TotalSurface = 120,
            LatestPrice = price,
            TypeBienId = typeBienId,
            Status = UnitCommercialStatus.Available
        };

    /// <summary>
    /// Reads the real projection the public catalogue is built from, rather
    /// than a copy of its rules, so a regression in the matching shows up here.
    /// </summary>
    private async Task<PublicPlanSummary?> PlanForTypeAsync(Guid projectId, int typeBienId)
    {
        await using var db = _fixture.CreateContext();
        var plans = await PublicCatalogueProjection.BuildPlansAsync(db, projectId, default);
        return plans.SingleOrDefault(p => p.TypeBienId == typeBienId);
    }

    [Fact]
    public async Task A_typed_unit_counts_only_towards_its_own_type()
    {
        RequireDatabase();
        var (projectId, apartmentTypeId, penthouseTypeId, floorId, immeubleId) =
            await SeedProjectWithTwoThreeBedroomTypesAsync();

        await using (var db = _fixture.CreateContext())
        {
            // Both are 3-bedroom units, so bedroom matching alone would count
            // each of them towards BOTH plans.
            db.Set<Unit>().AddRange(
                NewUnit(immeubleId, floorId, "A101", bedrooms: 3, typeBienId: apartmentTypeId, price: 1_600_000),
                NewUnit(immeubleId, floorId, "A102", bedrooms: 3, typeBienId: penthouseTypeId, price: 4_800_000));
            await db.SaveChangesAsync();
        }

        var apartmentPlan = await PlanForTypeAsync(projectId, apartmentTypeId);
        var penthousePlan = await PlanForTypeAsync(projectId, penthouseTypeId);

        apartmentPlan.Should().NotBeNull();
        penthousePlan.Should().NotBeNull();

        // The decisive assertion: the cheapest AVAILABLE unit of each plan is
        // its own unit, never the other type's. Under bedroom matching the
        // penthouse would have advertised the apartment's 1.6M price.
        apartmentPlan!.StartingPrice.Should().Be(1_600_000);
        penthousePlan!.StartingPrice.Should().Be(4_800_000);
    }

    [Fact]
    public async Task Untyped_stock_still_falls_back_to_the_bedroom_match()
    {
        RequireDatabase();
        var (projectId, apartmentTypeId, penthouseTypeId, floorId, immeubleId) =
            await SeedProjectWithTwoThreeBedroomTypesAsync();

        await using (var db = _fixture.CreateContext())
        {
            // No TypeBienId — the shape of every row created before the column.
            db.Set<Unit>().Add(
                NewUnit(immeubleId, floorId, "L101", bedrooms: 3, typeBienId: null, price: 2_100_000));
            await db.SaveChangesAsync();
        }

        // With nothing typed in the project, both 3-bedroom plans legitimately
        // match it — the pre-existing behaviour, preserved.
        (await PlanForTypeAsync(projectId, apartmentTypeId))!.StartingPrice.Should().Be(2_100_000);
        (await PlanForTypeAsync(projectId, penthouseTypeId))!.StartingPrice.Should().Be(2_100_000);
    }

    [Fact]
    public async Task Once_a_project_labels_its_stock_untyped_rows_stop_being_swept_in()
    {
        RequireDatabase();
        var (projectId, apartmentTypeId, penthouseTypeId, floorId, immeubleId) =
            await SeedProjectWithTwoThreeBedroomTypesAsync();

        await using (var db = _fixture.CreateContext())
        {
            db.Set<Unit>().AddRange(
                // Labelled: this is the apartment's real stock.
                NewUnit(immeubleId, floorId, "M101", bedrooms: 3, typeBienId: apartmentTypeId, price: 1_900_000),
                // Not labelled, same bedroom count, much cheaper. If the
                // fallback still applied per-plan, this would undercut both
                // plans' starting price and the penthouse would show 900k.
                NewUnit(immeubleId, floorId, "M102", bedrooms: 3, typeBienId: null, price: 900_000));
            await db.SaveChangesAsync();
        }

        var apartmentPlan = await PlanForTypeAsync(projectId, apartmentTypeId);
        var penthousePlan = await PlanForTypeAsync(projectId, penthouseTypeId);

        apartmentPlan!.StartingPrice.Should().Be(1_900_000);

        // The penthouse has no stock of its own and must not borrow the
        // unlabelled unit: it falls back to its own configured price (null
        // here), never to another layout's cheapest row.
        penthousePlan!.StartingPrice.Should().NotBe(900_000);
    }
}
