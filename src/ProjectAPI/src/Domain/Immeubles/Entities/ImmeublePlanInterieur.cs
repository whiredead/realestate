namespace ProjectAPI.Domain.Immeubles.Entities;

public class ImmeublePlanInterieur
{
    public Guid Id { get; set; }
    public Guid? ImmeubleId { get; set; }
    public List<string> PhotoLinks { get; set; } = new List<string>();

    public Immeuble? Immeuble { get; set; }
}
