namespace ProjectAPI.Api.Application.Projects.RemoveProjectFeatures;
public class RemoveProjectFeatureCommand : IRequest<bool>
{
    /// <summary>
    /// The project from which to remove features.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// The IDs of the features to remove.
    /// </summary>
    public List<Guid> FeatureIds { get; set; } = new();
}
