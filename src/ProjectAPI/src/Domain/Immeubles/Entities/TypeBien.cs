using ProjectAPI.Domain.Projects.Entities;

namespace ProjectAPI.Domain.Immeubles.Entities;

public class TypeBien
{
    /// <summary>
    /// Gets or sets the unique identifier of the Type de Bien.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the Type de Bien.
    /// </summary>
    public string Name { get; set; }
    public string? Description { get; set; }
    public double? Price { get; set; }
    public int? NbrChambre { get; set; }
    public int? NbrSalleDeBain { get; set; }
    public int? MinSurface { get; set; }
    public int? MaxSurface { get; set; }
    public string? ImagesInterieur { get; set; }


    /// <summary>
    /// Gets or sets the images of the Type de Bien.
    /// </summary>
    public string? Image { get; set; }
    public ICollection<ProjectTypeBien> Projects { get; set; } = new List<ProjectTypeBien>();
    public ICollection<ImmeubleTypeBien> Immeubles { get; set; } = new List<ImmeubleTypeBien>();

}
