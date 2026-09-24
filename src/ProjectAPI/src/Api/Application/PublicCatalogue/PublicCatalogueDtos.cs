namespace ProjectAPI.Api.Application.PublicCatalogue;

/// <summary>
/// §7.2 — the shapes the PUBLIC catalogue is allowed to see.
///
/// These exist as their own DTOs rather than reusing ProjectResponse or the
/// domain entities because the public site and the internal console want
/// genuinely different things, and the difference is a security boundary, not
/// a formatting preference. Nothing here carries an agent, a notary, a buyer,
/// a reservation, an immeuble id, a floor id, a unit id, a unit number, or a
/// stock count — a visitor sees commercial offers, not the CRM.
///
/// Anything added to these classes is, by definition, published to the
/// internet anonymously. Treat every new field as that decision.
/// </summary>
public static class PublicAvailability
{
    /// <summary>Freely selectable stock remains.</summary>
    public const string Available = "AVAILABLE";

    /// <summary>Stock is nearly gone — urgency, without publishing the number.</summary>
    public const string Last = "LAST_UNITS";

    /// <summary>Nothing selectable right now.</summary>
    public const string SoldOut = "UNAVAILABLE";

    /// <summary>
    /// Below this many matching units, the plan reads as "dernières
    /// disponibilités". The exact count is deliberately never published: it is
    /// internal stock data, and a competitor reading "2 left" learns more than
    /// a buyer needs to.
    /// </summary>
    public const int LastUnitsThreshold = 3;

    public static string FromCount(int available) => available switch
    {
        0 => SoldOut,
        <= LastUnitsThreshold => Last,
        _ => Available
    };
}

/// <summary>One plan card in the public catalogue: a TypeBien offered by a Project.</summary>
public class PublicPlanSummary
{
    /// <summary>Composite: a plan only means anything in the context of a project.</summary>
    public int TypeBienId { get; set; }
    public Guid ProjectId { get; set; }

    public string PlanName { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Commercial category — Studio, Appartement, Bureau, Commerce.</summary>
    public string PropertyType { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;
    public string? QuartierName { get; set; }
    public string? QuartierCity { get; set; }
    public string? Location { get; set; }

    /// <summary>The PROJECT's own description (rich text). Cards describe the programme, not the type de bien.</summary>
    public string? ProjectDescription { get; set; }

    /// <summary>The project's atouts (features), in the order the admin arranged them; capped for card payloads.</summary>
    public List<PublicFeature> ProjectFeatures { get; set; } = new();

    /// <summary>Friendly phase: SUR_PLAN | EN_LIVRAISON | FINALISE | SUSPENDED.</summary>
    public string Phase { get; set; } = string.Empty;

    /// <summary>"À partir de" — the plan's own price, else the cheapest matching unit.</summary>
    public decimal? StartingPrice { get; set; }

    public int? MinSurface { get; set; }
    public int? MaxSurface { get; set; }

    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public int? Showers { get; set; }

    /// <summary>Parking spaces included. Null = not recorded; the card omits it rather than claiming zero.</summary>
    public int? Parking { get; set; }

    /// <summary>The project's main picture (first project image), falling back to the plan's own picture.</summary>
    public string? CoverImage { get; set; }

    /// <summary>The type de bien's own picture (the plan itself), for the plan detail page.</summary>
    public string? PlanImage { get; set; }

    /// <summary>The parent project's coordinates (not the plan's own — a plan has no address of its own). Null when the project has none set.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>A band, never a count — see <see cref="PublicAvailability"/>.</summary>
    public string Availability { get; set; } = PublicAvailability.Available;

    /// <summary>Present only when the plan has its own 3D tour.</summary>
    public string? Module3DLink { get; set; }
}

public class PublicPlanPage
{
    public List<PublicPlanSummary> Data { get; set; } = new();
    public int TotalItems { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

/// <summary>One amenity or quartier feature card.</summary>
public class PublicFeature
{
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// A project as the public detail page needs it, in one round trip.
///
/// The page previously made four separate anonymous calls (project, features,
/// quartier amenities, videos) plus a 1000-row list scan to find the project
/// by id, because no public by-id route existed.
/// </summary>
public class PublicProjectDetail
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? Address { get; set; }

    /// <summary>SUR_PLAN | EN_LIVRAISON | FINALISE | SUSPENDED.</summary>
    public string Phase { get; set; } = string.Empty;

    /// <summary>Published only for SUR_PLAN projects — see the handler.</summary>
    public decimal? ConstructionProgress { get; set; }

    public decimal? StartingPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? MinSurface { get; set; }
    public int? MaxSurface { get; set; }

    public List<string> Images { get; set; } = new();
    public List<string> Videos { get; set; } = new();
    public string? Module3DLink { get; set; }

    /// <summary>Real map coordinates. Null when the project has none set — the public map skips it rather than guessing a pin.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string? QuartierName { get; set; }
    public string? QuartierCity { get; set; }
    public string? QuartierDescription { get; set; }
    public List<string> QuartierImages { get; set; } = new();

    /// <summary>What the development itself offers (pool, mosque, …).</summary>
    public List<PublicFeature> Amenities { get; set; } = new();

    /// <summary>What is nearby (schools, transport, …).</summary>
    public List<PublicFeature> QuartierFeatures { get; set; } = new();

    /// <summary>The plans this project offers, same shape as a catalogue card.</summary>
    public List<PublicPlanSummary> Plans { get; set; } = new();

    /// <summary>Distinct commercial categories on offer, for the quick-facts strip.</summary>
    public List<string> PropertyTypes { get; set; } = new();

    /// <summary>
    /// The assigned agent's first name only — never a last name, email, phone
    /// or user id. Null when no agent is assigned yet. The one deliberate
    /// exception to this file's own rule above ("nothing here carries an
    /// agent"): a name puts a human face on the visit-request ask without
    /// publishing a directory entry. The actual contact action still goes
    /// through the company's published number, not a personal one.
    /// </summary>
    public string? AgentFirstName { get; set; }

    /// <summary>How many visitors (signed in or anonymous) have favourited this project — Project.NumberLikes, published as a simple interest count.</summary>
    public int InterestCount { get; set; }
}
