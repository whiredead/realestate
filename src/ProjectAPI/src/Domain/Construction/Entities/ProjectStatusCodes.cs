namespace ProjectAPI.Domain.Construction.Entities;

/// <summary>
/// Project lifecycle (spec §7.5): exactly three statuses.
///
/// <list type="bullet">
///   <item><c>SUR_PLAN</c> — sold off-plan, under construction: reservations are open.</item>
///   <item><c>EN_LIVRAISON</c> — construction at 100 %, units are being delivered: no new
///   reservation, approved files continue (final visit, sale, notary, handover).</item>
///   <item><c>FINALISE</c> — closed: read-only.</item>
/// </list>
///
/// <c>Project.StatusGlobal</c> stores one of these three codes (migration
/// <c>ProjectStatusThreeValues</c> converted existing rows). Older spellings
/// ("CommingSoon", "UnderConstruction", "PLANNED", "COMPLETED", "ARCHIVED"…) are
/// still accepted on input and mapped by <see cref="Normalize"/>.
///
/// The status only moves forward, through commands: "Passer en livraison"
/// (100 % of the weighted milestones) then "Finaliser le projet".
/// </summary>
public static class ProjectStatusCodes
{
    public const string SurPlan = "SUR_PLAN";
    public const string EnLivraison = "EN_LIVRAISON";
    public const string Finalise = "FINALISE";

    /// <summary>The three stored values, in lifecycle order.</summary>
    public static readonly string[] All = { SurPlan, EnLivraison, Finalise };

    /// <summary>Maps any accepted spelling to one of the three codes. Unknown values are SUR_PLAN.</summary>
    public static string Normalize(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return SurPlan;

        return status.Trim().ToUpperInvariant().Replace(' ', '_') switch
        {
            "SUR_PLAN" or "SURPLAN" or "DRAFT" or "PLANNED" or "COMINGSOON" or "COMMINGSOON" or "COMING_SOON"
                or "IN_PROGRESS" or "INPROGRESS" or "UNDERCONSTRUCTION" or "UNDER_CONSTRUCTION"
                or "AVAILABLE" or "SOLD" or "SUSPENDED" => SurPlan,

            "EN_LIVRAISON" or "ENLIVRAISON" or "COMPLETED" or "DELIVERED" => EnLivraison,

            "FINALISE" or "FINALISÉ" or "FINALISED" or "FINALIZED" or "ARCHIVED" => Finalise,

            _ => SurPlan
        };
    }

    /// <summary>Kept for callers that speak of the "business phase": it is the status itself.</summary>
    public static class Phase
    {
        public const string SurPlan = ProjectStatusCodes.SurPlan;
        public const string EnLivraison = ProjectStatusCodes.EnLivraison;
        public const string Finalise = ProjectStatusCodes.Finalise;
    }

    /// <summary>The business phase is the status (the three values are the phases).</summary>
    public static string GetBusinessPhase(string? status) => Normalize(status);

    /// <summary>New reservations only while the project is sold off-plan.</summary>
    public static bool AllowsNewReservation(string? status) => Normalize(status) == SurPlan;

    /// <summary>A final visit is only requested once the project is in delivery (§17.1 FR-FVI-001).</summary>
    public static bool AllowsFinalVisit(string? status) => Normalize(status) == EnLivraison;

    /// <summary>A finalised project accepts no business mutation.</summary>
    public static bool IsReadOnly(string? status) => Normalize(status) == Finalise;

    /// <summary>
    /// Building (Immeuble) statuses keep their own vocabulary, which used to be
    /// normalised by the project function: DRAFT, PLANNED, IN_PROGRESS,
    /// SUSPENDED, COMPLETED, ARCHIVED.
    /// </summary>
    public static string NormalizeBuildingStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return "DRAFT";

        return status.Trim().ToUpperInvariant() switch
        {
            "DRAFT" => "DRAFT",
            "PLANNED" or "COMINGSOON" or "COMMINGSOON" or "COMING_SOON" => "PLANNED",
            "IN_PROGRESS" or "INPROGRESS" or "UNDERCONSTRUCTION" or "UNDER_CONSTRUCTION"
                or "AVAILABLE" or "SOLD" => "IN_PROGRESS",
            "SUSPENDED" => "SUSPENDED",
            "COMPLETED" or "DELIVERED" => "COMPLETED",
            "ARCHIVED" => "ARCHIVED",
            _ => "DRAFT"
        };
    }
}
