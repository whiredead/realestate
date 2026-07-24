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
    /// </summary>
    public static string Normalize(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return Draft;

        return status.Trim().ToUpperInvariant() switch
        {
            "DRAFT" => Draft,

            // "Coming soon" (and its misspelling) means planned but not started.
            "PLANNED" or "COMINGSOON" or "COMMINGSOON" or "COMING_SOON" => Planned,

            "IN_PROGRESS" or "INPROGRESS" or "UNDERCONSTRUCTION" or "UNDER_CONSTRUCTION" => InProgress,

            "SUSPENDED" => Suspended,

            // A project that is finished and sellable is COMPLETED.
            "COMPLETED" or "AVAILABLE" or "DELIVERED" => Completed,

            "ARCHIVED" or "SOLD" => Archived,

            _ => Draft
        };
    }

    /// <summary>
    /// True when the project allows a final-visit request (§7.5 / §17.1).
    /// </summary>
    public static bool AllowsFinalVisit(string? status) => Normalize(status) == Completed;
}
