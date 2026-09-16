namespace ProjectAPI.Domain.Common.DTOs;
public class TypeBienListItem
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? Image { get; set; }
    public double? Price { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? NbrChambre { get; set; }
    public int? NbrSalleDeBain { get; set; }
    public int? NbrDouche { get; set; }
    public int? NbrParking { get; set; }
    public string? Module3DLink { get; set; }
    public int? MinSurface { get; set; }
    public int? MaxSurface { get; set; }
    public string? SurfaceRange { get; set; }
    public List<string> ImagesInterieur { get; set; }
    public List<Guid> ImmeubleIds { get; set; } = new();
    public List<Guid> ProjectIds { get; set; } = new();
}

