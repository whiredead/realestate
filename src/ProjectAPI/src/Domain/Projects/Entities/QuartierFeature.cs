namespace ProjectAPI.Domain.Projects.Entities;

/// <summary>
/// One feature of a quartier (§7.2 "Quartier" referential) — e.g. "Parc
/// urbain", "Tramway": a title, a description and an optional image. Belongs to
/// the quartier itself, so every project attached to it shows the same list.
/// </summary>
public class QuartierFeature
{
    public Guid Id { get; set; }

    public Guid QuartierId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Optional image URL (validated by MediaUrlPolicy).</summary>
    public string? Image { get; set; }

    /// <summary>Display order within the quartier.</summary>
    public int SequenceNo { get; set; }

    public DateTime CreatedAt { get; set; }
}
