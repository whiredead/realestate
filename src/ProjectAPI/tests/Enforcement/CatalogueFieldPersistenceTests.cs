using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Sales.Entities;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// Scope C — proves the new catalogue/warranty columns survive a round trip
/// through the real schema, and that the two defaults that matter are the ones
/// the migrations actually wrote.
///
/// These are database tests rather than unit tests on purpose: every one of
/// these fields is a column added by a migration, and a property that compiles
/// but whose column is missing, non-nullable, or defaulted differently is
/// exactly the failure an in-memory provider hides.
/// </summary>
[Collection("sqlserver")]
public class CatalogueFieldPersistenceTests
{
    private readonly SqlServerFixture _fixture;

    public CatalogueFieldPersistenceTests(SqlServerFixture fixture) => _fixture = fixture;

    private void RequireDatabase()
    {
        if (!_fixture.Available)
        {
            throw new InvalidOperationException(
                "These tests assert real columns and defaults and cannot run without SQL Server. " +
                $"Connection failed: {_fixture.UnavailableReason}");
        }
    }

    private static Project NewProject(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Location = "Casablanca",
        Address = "1 rue de test",
        Description = "test",
        Module3DLink = string.Empty,
        StatusGlobal = "DRAFT"
    };

    // ----------------------------------------------------------- §8 warranty

    [Fact]
    public async Task Project_warranty_defaults_to_twelve_months()
    {
        RequireDatabase();

        // The figure StartHandoverHandler used to hard-code. A project created
        // without naming a warranty must land on it, or the migration silently
        // shortened every existing project's SAV window to zero.
        var id = Guid.NewGuid();
        await using (var db = _fixture.CreateContext())
        {
            var project = NewProject("Warranty-default");
            project.Id = id;
            db.Add(project);
            await db.SaveChangesAsync();
        }

        await using (var db = _fixture.CreateContext())
        {
            var stored = await db.Set<Project>().AsNoTracking().SingleAsync(p => p.Id == id);
            stored.WarrantyMonths.Should().Be(12);
        }
    }

    [Fact]
    public async Task Project_warranty_round_trips_a_custom_value()
    {
        RequireDatabase();

        var id = Guid.NewGuid();
        await using (var db = _fixture.CreateContext())
        {
            var project = NewProject("Warranty-custom");
            project.Id = id;
            project.WarrantyMonths = 24;
            db.Add(project);
            await db.SaveChangesAsync();
        }

        await using (var db = _fixture.CreateContext())
        {
            var stored = await db.Set<Project>().AsNoTracking().SingleAsync(p => p.Id == id);
            stored.WarrantyMonths.Should().Be(24);
        }
    }

    [Fact]
    public async Task Changing_a_projects_warranty_does_not_reach_an_existing_sale()
    {
        RequireDatabase();

        // §8 — the whole point of freezing WarrantyMonths onto the sale. If the
        // handover read the project instead, editing this field would silently
        // lengthen or shorten warranties already granted to buyers.
        var projectId = Guid.NewGuid();
        var saleId = Guid.NewGuid();

        await using (var db = _fixture.CreateContext())
        {
            var project = NewProject("Warranty-freeze");
            project.Id = projectId;
            project.WarrantyMonths = 12;
            db.Add(project);

            // Sales.UnitId is a real FK, so the sale needs a real unit behind it
            // (immeuble → floor → unit; Unit.ProjectId holds the IMMEUBLE id).
            var immeuble = new Immeuble
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Name = "Immeuble W",
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
                UnitNumber = "W101",
                View = "Jardin",
                Orientation = "Nord",
                Status = UnitCommercialStatus.Available
            };

            db.Add(immeuble);
            db.Add(floor);
            db.Add(unit);

            db.Add(new Sale
            {
                Id = saleId,
                UnitId = unit.Id,
                BuyerFirstName = "Test",
                BuyerLastName = "Buyer",
                BuyerEmail = "buyer@test.local",
                BuyerPhoneNumber = "0600000000",
                SaleDate = DateTime.UtcNow,
                TotalPrice = 1_000_000m,
                Status = SaleStatus.Draft,
                WarrantyMonths = project.WarrantyMonths
            });

            await db.SaveChangesAsync();
        }

        await using (var db = _fixture.CreateContext())
        {
            var project = await db.Set<Project>().SingleAsync(p => p.Id == projectId);
            project.WarrantyMonths = 36;
            await db.SaveChangesAsync();
        }

        await using (var db = _fixture.CreateContext())
        {
            var sale = await db.Set<Sale>().AsNoTracking().SingleAsync(s => s.Id == saleId);
            sale.WarrantyMonths.Should().Be(12, "the sale froze its warranty at creation");
        }
    }

    // -------------------------------------------------- §7.2 TypeBien fields

    [Fact]
    public async Task TypeBien_3d_link_and_shower_count_round_trip()
    {
        RequireDatabase();

        int id;
        await using (var db = _fixture.CreateContext())
        {
            var type = new TypeBien
            {
                Name = "T3-3D",
                NbrChambre = 3,
                NbrSalleDeBain = 1,
                NbrDouche = 2,
                Module3DLink = "https://tour.example/t3"
            };
            db.Add(type);
            await db.SaveChangesAsync();
            id = type.Id;
        }

        await using (var db = _fixture.CreateContext())
        {
            var stored = await db.Set<TypeBien>().AsNoTracking().SingleAsync(t => t.Id == id);
            stored.NbrDouche.Should().Be(2);
            stored.NbrSalleDeBain.Should().Be(1, "showers are counted apart from bathrooms");
            stored.Module3DLink.Should().Be("https://tour.example/t3");
        }
    }

    [Fact]
    public async Task TypeBien_new_fields_stay_null_when_not_recorded()
    {
        RequireDatabase();

        // The migration adds no back-fill: 0 showers and "nobody filled this in"
        // are different statements, and only the second is true of existing rows.
        int id;
        await using (var db = _fixture.CreateContext())
        {
            var type = new TypeBien { Name = "T2-legacy" };
            db.Add(type);
            await db.SaveChangesAsync();
            id = type.Id;
        }

        await using (var db = _fixture.CreateContext())
        {
            var stored = await db.Set<TypeBien>().AsNoTracking().SingleAsync(t => t.Id == id);
            stored.NbrDouche.Should().BeNull();
            stored.Module3DLink.Should().BeNull();
        }
    }
}
