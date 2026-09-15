namespace ProjectAPI.Api.Application.Common.Models;

public class ProjectFeatureResponse
{
    /// <summary>
    /// Gets or sets the unique identifier of the project feature.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the feature (e.g., Swimming Pool, Mosque, Playground).
    /// </summary>
    public string Name { get; set; }
    public string Icon { get; set; }

    /// <summary>One-line selling point ("Nos atouts") shown under the name.</summary>
    public string? Description { get; set; }

    public int SequenceNo { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the related project.
    /// </summary>
    public Guid ProjectId { get; set; }
}
