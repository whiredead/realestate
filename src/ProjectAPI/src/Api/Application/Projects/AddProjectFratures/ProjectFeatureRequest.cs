namespace ProjectAPI.Api.Application.Projects.AddProjectFratures;

public class ProjectFeatureRequest
{
    public string Name { get; set; }
    public string Icon { get; set; }

    /// <summary>One-line selling point ("Nos atouts"), optional.</summary>
    public string? Description { get; set; }
}
