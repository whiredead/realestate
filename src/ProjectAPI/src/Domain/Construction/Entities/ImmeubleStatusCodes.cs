namespace ProjectAPI.Domain.Construction.Entities;

/// <summary>
/// Building (immeuble) status: exactly two values. A building is either still being built or built.
///
/// This is deliberately NOT the project lifecycle (<see cref="ProjectStatusCodes"/>: sur plan / en
/// livraison / finalisé). Those describe how a programme is being sold; a building only has a
/// construction state, and showing "Sur plan" next to "En livraison" on a building contradicted the
/// project it belongs to.
/// </summary>
public static class ImmeubleStatusCodes
{
    public const string UnderConstruction = "EN_COURS_DE_CONSTRUCTION";
    public const string Built = "CONSTRUIT";

    public static readonly string[] All = { UnderConstruction, Built };

    /// <summary>
    /// Maps any accepted spelling to one of the two codes. The old project-phase values are still
    /// understood so older rows and clients keep working: en livraison / finalisé mean the building
    /// is built; sur plan and anything unknown mean it is still under construction.
    /// </summary>
    public static string Normalize(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return UnderConstruction;

        return status.Trim().ToUpperInvariant().Replace(' ', '_') switch
        {
            "CONSTRUIT" or "CONSTRUITE" or "BUILT" or "COMPLETED" or "DELIVERED"
                or "EN_LIVRAISON" or "ENLIVRAISON" or "FINALISE" or "FINALISÉ" or "FINALISED" or "FINALIZED" or "ARCHIVED" => Built,

            _ => UnderConstruction
        };
    }

    /// <summary>The status a new building starts with, derived from its project's phase.</summary>
    public static string ForNewBuilding(string? projectStatus) =>
        ProjectStatusCodes.Normalize(projectStatus) == ProjectStatusCodes.SurPlan ? UnderConstruction : Built;
}
