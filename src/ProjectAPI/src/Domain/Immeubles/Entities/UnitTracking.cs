namespace ProjectAPI.Domain.Immeubles.Entities;
/// <summary>
/// Represents the tracking of the project, especially for projects under construction.
/// </summary>
public class UnitTracking
{
    public Guid Id { get; set; }
    public Guid UnitId { get; set; }
    public string CurrentPhase { get; set; } // e.g., Foundation, Walls, Finishing
    public string ProgressDescription { get; set; } // Description of the current progress
    public ICollection<string> Images { get; set; } = new List<string>(); // Images showing the progress
    public DateTime LastUpdated { get; set; }
    public Unit Unit { get; set; }
}
