namespace ProjectAPI.Domain.Projects.Entities;

/// <summary>
/// Display/reference data for the fixed project lifecycle codes. Codes remain
/// immutable because they are used by the construction workflow; admins can
/// maintain their wording, order and availability from the CRM.
/// </summary>
public class ProjectStatusReference
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>One of the protected lifecycle phases: SUR_PLAN, EN_LIVRAISON or FINALISE.</summary>
    public string BusinessPhase { get; set; } = "SUR_PLAN";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
