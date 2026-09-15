namespace ProjectAPI.Api.Application.Projects.DeleteProjectFeature;

/// <summary>DELETE /api/Projects/features/{id} — removes one "atout".</summary>
public class DeleteProjectFeatureCommand : IRequest<MediatR.Unit>
{
    public Guid Id { get; set; }
}
