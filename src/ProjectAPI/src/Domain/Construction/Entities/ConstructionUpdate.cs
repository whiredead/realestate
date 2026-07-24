namespace ProjectAPI.Domain.Construction.Entities;

/// <summary>
/// A published progress update (spec §15.2, §48.7).
///
/// Published updates are NEVER overwritten (§15.2 FR-CON-004): a correction
/// creates a new version, so the history a buyer already saw stays intact.
/// </summary>
public class ConstructionUpdate
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    /// <summary>1-based; increments with each correction.</summary>
    public int VersionNo { get; set; } = 1;

    /// <summary>Overall progress declared by this update, 0..100.</summary>
    public decimal ProgressPercent { get; set; }

    public string TitleFr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string? DescriptionFr { get; set; }
    public string? DescriptionEn { get; set; }

    /// <summary>Comma-separated media URLs (photos, videos).</summary>
    public string? MediaUrls { get; set; }

    public UpdateVisibility Visibility { get; set; } = UpdateVisibility.Buyers;

    /// <summary>Update this one revises, when it is a correction.</summary>
    public Guid? SupersedesId { get; set; }

    public string? AuthorUserId { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Who may see an update (§15.2).</summary>
public enum UpdateVisibility
{
    /// <summary>Internal only.</summary>
    Internal = 0,

    /// <summary>Visible to buyers of the project.</summary>
    Buyers = 1,

    /// <summary>Visible on the public site.</summary>
    Public = 2
}
