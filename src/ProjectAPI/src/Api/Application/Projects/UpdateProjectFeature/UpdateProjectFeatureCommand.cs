using ProjectAPI.Api.Application.Common.Models;

namespace ProjectAPI.Api.Application.Projects.UpdateProjectFeature;

/// <summary>PUT /api/Projects/features/{id} — edits one "atout" (name, icon, one-line description).</summary>
public class UpdateProjectFeatureCommand : IRequest<ProjectFeatureResponse>
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Icon { get; set; }
    public string? Description { get; set; }
}
