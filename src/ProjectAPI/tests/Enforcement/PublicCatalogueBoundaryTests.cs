using System.Reflection;
using FluentAssertions;
using ProjectAPI.Api.Application.PublicCatalogue;

namespace ProjectAPI.Tests.Enforcement;

/// <summary>
/// Phase 2 — guards the anonymous public boundary.
///
/// Everything reachable from a PublicCatalogue DTO is published to the internet
/// without a login. The risk is not that someone writes
/// <c>public string AgentName</c> on purpose; it is that someone adds a
/// navigation property to a shared type and a whole entity graph follows it out
/// through a serialiser. These tests walk the DTOs' reachable property graph
/// and fail if an internal concept appears in it.
/// </summary>
public class PublicCatalogueBoundaryTests
{
    /// <summary>
    /// Names that must never appear on the public surface. Buyer/agent/notary
    /// identities, the CRM hierarchy below a project, and raw stock.
    /// </summary>
    private static readonly string[] ForbiddenFragments =
    {
        "Agent", "Notaire", "Notary", "Buyer", "Reservation", "Sale",
        "Immeuble", "Floor", "UnitId", "UnitNumber", "Claim", "Warranty",
        "Payment", "Purchase", "Contact", "Membership", "Audit"
    };

    /// <summary>
    /// Deliberate exceptions: a name that merely CONTAINS a forbidden fragment
    /// but exposes nothing internal.
    /// </summary>
    private static readonly string[] Allowed =
    {
        // "Availability" contains no forbidden fragment; listed here as the
        // place to justify any future exception rather than weakening the rule.
    };

    private static IEnumerable<Type> PublicDtoRoots() => new[]
    {
        typeof(PublicPlanSummary),
        typeof(PublicPlanPage),
        typeof(PublicPlanDetail),
        typeof(PublicProjectDetail),
        typeof(PublicFeature),
        typeof(PublicFilterOptions),
        typeof(PublicFilterOption)
    };

    /// <summary>Every property name reachable from the given roots, following nested DTOs.</summary>
    private static IEnumerable<(Type Owner, string Property, Type PropertyType)> ReachableProperties()
    {
        var seen = new HashSet<Type>();
        var queue = new Queue<Type>(PublicDtoRoots());

        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            if (!seen.Add(type)) continue;

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                yield return (type, prop.Name, prop.PropertyType);

                // Follow anything declared in our own assembly (nested DTOs and
                // collections of them); primitives and framework types stop here.
                var candidate = Unwrap(prop.PropertyType);
                if (candidate.Assembly == typeof(PublicPlanSummary).Assembly)
                {
                    queue.Enqueue(candidate);
                }
            }
        }
    }

    private static Type Unwrap(Type type)
    {
        if (type.IsGenericType)
        {
            var arg = type.GetGenericArguments().FirstOrDefault();
            if (arg is not null) return Unwrap(arg);
        }
        return type;
    }

    [Fact]
    public void No_internal_concept_is_reachable_from_a_public_dto()
    {
        var offenders = ReachableProperties()
            .Where(p => !Allowed.Contains(p.Property, StringComparer.Ordinal))
            .Where(p => ForbiddenFragments.Any(f => p.Property.Contains(f, StringComparison.Ordinal)))
            .Select(p => $"{p.Owner.Name}.{p.Property}")
            .ToList();

        offenders.Should().BeEmpty(
            "a public catalogue DTO must not expose CRM internals — these are served anonymously");
    }

    [Fact]
    public void No_domain_entity_is_reachable_from_a_public_dto()
    {
        // The stronger version of the rule above: a DTO must not reference a
        // domain entity at all, whatever it is called. One navigation property
        // is enough to drag an entity graph out through the serialiser.
        var entityLeaks = ReachableProperties()
            .Select(p => (p.Owner, p.Property, Type: Unwrap(p.PropertyType)))
            .Where(p => p.Type.Namespace?.StartsWith("ProjectAPI.Domain", StringComparison.Ordinal) == true)
            .Select(p => $"{p.Owner.Name}.{p.Property} -> {p.Type.FullName}")
            .ToList();

        entityLeaks.Should().BeEmpty("public DTOs must be standalone, never a window onto the domain model");
    }

    [Fact]
    public void Availability_is_published_as_a_band_never_a_count()
    {
        // Publishing "2 units left" hands a competitor real stock data. The band
        // gives a shopper the same urgency without the number.
        var properties = typeof(PublicPlanSummary).GetProperties().Select(p => p.Name).ToList();

        properties.Should().Contain("Availability");
        properties.Should().NotContain(n =>
            n.Contains("Count", StringComparison.Ordinal) || n.Contains("Stock", StringComparison.Ordinal));

        typeof(PublicPlanSummary).GetProperty("Availability")!
            .PropertyType.Should().Be(typeof(string));
    }

    [Theory]
    [InlineData(0, PublicAvailability.SoldOut)]
    [InlineData(1, PublicAvailability.Last)]
    [InlineData(3, PublicAvailability.Last)]
    [InlineData(4, PublicAvailability.Available)]
    [InlineData(50, PublicAvailability.Available)]
    public void The_availability_band_matches_the_remaining_stock(int available, string expected)
    {
        PublicAvailability.FromCount(available).Should().Be(expected);
    }

    [Fact]
    public void The_filter_options_carry_only_an_id_a_name_and_a_quartier()
    {
        // The catalogue filters used to be populated from GET /api/Projects,
        // whose internal projection hands an anonymous caller AssignedAgents
        // and AssignedNotaries with e-mail addresses and phone numbers — the
        // staff directory, served to render two dropdowns. This DTO exists so
        // that cannot happen again, and the assertion pins its whole surface.
        var props = typeof(PublicFilterOption).GetProperties().Select(p => p.Name).OrderBy(n => n);

        props.Should().Equal("Id", "Name", "QuartierName");
    }

    [Fact]
    public void The_filter_response_is_three_flat_lists()
    {
        // Flat lists of primitives cannot drag an entity graph behind them.
        var props = typeof(PublicFilterOptions).GetProperties();

        props.Select(p => p.Name).OrderBy(n => n)
            .Should().Equal("Projects", "PropertyTypes", "Quartiers");

        typeof(PublicFilterOptions).GetProperty("Quartiers")!.PropertyType
            .Should().Be(typeof(List<string>));
        typeof(PublicFilterOptions).GetProperty("PropertyTypes")!.PropertyType
            .Should().Be(typeof(List<string>));
    }

    [Theory]
    [InlineData("Studio 35m²", null, "Studio")]
    [InlineData("T3", "Bureaux", "Bureau")]
    [InlineData("Local", "Magasin et Commerce", "Commerce")]
    // "commercial" is not a superstring of "commerce": they diverge at the
    // seventh letter, so this exact name silently classified as Appartement.
    [InlineData("Local commercial", null, "Commerce")]
    [InlineData("Boutique", null, "Commerce")]
    [InlineData("T4 duplex", "Livraison immédiate", "Appartement")]
    public void Plans_are_classified_into_the_four_public_categories(
        string planName, string? projectType, string expected)
    {
        // The referential has no category column, so the category is inferred
        // once here rather than in every component that renders a card.
        PublicCatalogueProjection.ClassifyPropertyType(planName, projectType).Should().Be(expected);
    }
}
