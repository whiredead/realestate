namespace ProjectAPI.Domain.Construction.Entities;

/// <summary>
/// Project lifecycle codes (spec §7.5).
///
/// <c>Project.StatusGlobal</c> is a free-text column carrying legacy values
/// ("CommingSoon", "UnderConstruction", "Available"…). Rather than migrate the
/// column and break existing data and the frontend, this class defines the
/// canonical codes and maps the legacy spellings onto them.
///
/// The critical rule: a final-visit request is only allowed when the project is
/// COMPLETED (§7.5, §17.1 FR-FVI-001).
/// </summary>
public static class ProjectStatusCodes
{
    public const string Draft = "DRAFT";
    public const string Planned = "PLANNED";
    public const string InProgress = "IN_PROGRESS";
    public const string Suspended = "SUSPENDED";
    public const string Completed = "COMPLETED";
    public const string Archived = "ARCHIVED";

    /// <summary>
    /// Normalises a stored value to a canonical code. Legacy spellings — including
    /// the misspelled "CommingSoon" default — are mapped rather than rejected.
    ///
    /// A mapping must never manufacture a state that unlocks a gate. COMPLETED is
    /// the gate for final-visit requests (<see cref="AllowsFinalVisit"/>), so the
    /// legacy commercial values must not reach it:
    ///   - "Available" means units are on sale. It asserts nothing about
    ///     construction, so it maps to IN_PROGRESS, not COMPLETED.
    ///   - "Sold" means sold out — also a commercial fact, and not ARCHIVED.
    /// Both are LOSSY on purpose: a genuinely finished project becomes COMPLETED
    /// only by passing <c>CompleteProjectCommand</c> (§15.3), which checks 100%
    /// weighted progress, an explicit confirmation and a real end date.
    /// The frontend carries the matching correction (src/api/http/enums.ts).
    /// </summary>
    public static string Normalize(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return Draft;

        return status.Trim().ToUpperInvariant() switch
        {
            "DRAFT" => Draft,

            // "Coming soon" (and its misspelling) means planned but not started.
            "PLANNED" or "COMINGSOON" or "COMMINGSOON" or "COMING_SOON" => Planned,

            // Legacy commercial states collapse here: neither says the build is done.
            "IN_PROGRESS" or "INPROGRESS" or "UNDERCONSTRUCTION" or "UNDER_CONSTRUCTION"
                or "AVAILABLE" or "SOLD" => InProgress,

            "SUSPENDED" => Suspended,

            // Only the canonical code — and DELIVERED, which does assert that the
            // build finished and was handed over — count as COMPLETED.
            "COMPLETED" or "DELIVERED" => Completed,

            "ARCHIVED" => Archived,

            // Unknown values fail closed: DRAFT unlocks nothing.
            _ => Draft
        };
    }

    /// <summary>
    /// True when the project allows a final-visit request (§7.5 / §17.1).
    ///
    /// COMPLETED is the EN_LIVRAISON phase: construction is finished and the
    /// approved files move on to visit, sale, notary and handover.
    /// </summary>
    public static bool AllowsFinalVisit(string? status) => Normalize(status) == Completed;

    /// <summary>
    /// Commercial phase a stored status belongs to.
    ///
    /// The phases are a VIEW over the persisted codes, not a second column:
    /// nothing is migrated, and <see cref="Normalize"/> stays the only place
    /// legacy spellings are interpreted.
    /// </summary>
    public static class Phase
    {
        /// <summary>Selling off-plan: new reservations are accepted.</summary>
        public const string SurPlan = "SUR_PLAN";

        /// <summary>Built and handing over: no new reservations, existing files proceed.</summary>
        public const string EnLivraison = "EN_LIVRAISON";

        /// <summary>Closed: consultation only.</summary>
        public const string Finalise = "FINALISE";

        /// <summary>
        /// Administratively withdrawn. Deliberately NOT one of the three business
        /// phases: it is reversible and blocks mutations without asserting where
        /// the project sits in its commercial life.
        /// </summary>
        public const string Suspended = "SUSPENDED";
    }

    /// <summary>
    /// Maps a stored status onto its business phase.
    ///
    ///   DRAFT / PLANNED / IN_PROGRESS -> SUR_PLAN
    ///   COMPLETED                     -> EN_LIVRAISON
    ///   ARCHIVED                      -> FINALISE
    ///   SUSPENDED                     -> SUSPENDED
    /// </summary>
    public static string GetBusinessPhase(string? status) => Normalize(status) switch
    {
        Completed => Phase.EnLivraison,
        Archived => Phase.Finalise,
        Suspended => Phase.Suspended,
        _ => Phase.SurPlan
    };

    /// <summary>
    /// True when a NEW reservation may be created on this project — SUR_PLAN only.
    ///
    /// A project in EN_LIVRAISON keeps serving its existing approved files; it
    /// simply stops taking new ones.
    /// </summary>
    public static bool AllowsNewReservation(string? status) =>
        GetBusinessPhase(status) == Phase.SurPlan;

    /// <summary>
    /// True when the project accepts no business mutation at all: FINALISE
    /// (closed) and SUSPENDED (withdrawn) are both consultation-only.
    ///
    /// Callers should treat this as a hard gate BEFORE any other rule, so a
    /// closed project cannot be mutated through a path that only checks its own
    /// narrower precondition.
    /// </summary>
    public static bool IsReadOnly(string? status)
    {
        var phase = GetBusinessPhase(status);
        return phase is Phase.Finalise or Phase.Suspended;
    }
}
