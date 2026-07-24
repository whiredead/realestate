namespace ProjectAPI.Domain.Immeubles.Entities;

public class ImmeubleTypeBien
{
    public Guid Id { get; set; }
    public Guid? ImmeubleId { get; set; }
    public int? TypeBienId { get; set; }
    public Immeuble? Immeuble { get; set; }
    public TypeBien? TypeBien { get; set; }
}
