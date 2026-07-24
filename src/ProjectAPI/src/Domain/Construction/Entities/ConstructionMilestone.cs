namespace ProjectAPI.Domain.Construction.Entities;

/// <summary>
/// A weighted construction milestone for a project (spec §15.1, §48.7).
///
/// The weights of a project's milestones express how much each one contributes
/// to overall progress; the spec caps a weight at 0..100.
/// </summary>
public class ConstructionMilestone
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    /// <summary>Stable business code, unique within the project.</summary>
    public string Code { get; set; } = string.Empty;

    public string NameFr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? DescriptionFr { get; set; }
    public string? DescriptionEn { get; set; }

    /// <summary>Display order.</summary>
    public int SequenceNo { get; set; }

    /// <summary>Contribution to overall progress, 0..100 (§48.7).</summary>
    public decimal WeightPercent { get; set; }

    public DateTime? PlannedDate { get; set; }
    public DateTime? ActualDate { get; set; }

    public MilestoneStatus Status { get; set; } = MilestoneStatus.NotStarted;

    /// <summary>Visible in the buyer area.</summary>
    public bool VisibleToBuyer { get; set; } = true;

    /// <summary>Visible on the public site.</summary>
    public bool VisibleToPublic { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Milestone lifecycle (§15.1 FR-CON-002).</summary>
public enum MilestoneStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2,
    Delayed = 3,
    Suspended = 4
}
