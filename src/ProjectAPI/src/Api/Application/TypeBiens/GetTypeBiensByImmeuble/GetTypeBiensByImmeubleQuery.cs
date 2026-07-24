namespace ProjectAPI.Api.Application.TypeBiens.GetTypeBiensByImmeuble;

/// <summary>
/// Query to retrieve TypeBiens linked to a specific Immeuble.
/// </summary>
public class GetTypeBiensByImmeubleQuery : IRequest<List<TypeBienListItem>>
{
    public Guid? ImmeubleId { get; set; }
}

/// <summary>
/// Represents the result item of a TypeBien when listing them for an Immeuble.
/// </summary>
public class TypeBienListItem
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? Image { get; set; }
    public double? Price { get; set; }
    public int? NbrChambre { get; set; }
    public int? NbrSalleDeBain { get; set; }
    public int? MinSurface { get; set; }
    public int? MaxSurface { get; set; }
    public string? SurfaceRange { get; set; }
    public List<string> ImagesInterieur { get; set; }
    public List<Guid> ImmeubleIds { get; set; } = new();
    public List<Guid> ProjectIds { get; set; } = new();
}
