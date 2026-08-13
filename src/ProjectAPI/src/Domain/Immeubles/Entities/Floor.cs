namespace ProjectAPI.Domain.Immeubles.Entities;

/// <summary>
/// A floor/level within a building (project-creation spec, real-estate
/// hierarchy: Project → Building (Immeuble) → Floor → Unit). Previously a
/// bare label typed onto each Unit — modeled as its own entity so a floor can
/// be created once per building and its units ordered/grouped under it.
/// </summary>
public class Floor
{
    public Guid Id { get; set; }

    public Guid ImmeubleId { get; set; }
    public Immeuble Immeuble { get; set; } = null!;

    /// <summary>Display label, e.g. "Ground Floor", "1st Floor" (legacy Unit.Floor values become these).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Ordering within the building — ground floor first, then ascending.</summary>
    public int SequenceNo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Unit> Units { get; set; } = new List<Unit>();
}
