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
    /// </summary>
    public static bool AllowsFinalVisit(string? status) => Normalize(status) == Completed;
}
