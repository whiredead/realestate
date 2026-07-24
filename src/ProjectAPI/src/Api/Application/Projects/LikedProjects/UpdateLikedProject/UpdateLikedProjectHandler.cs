using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.Projects.LikedProjects.UpdateLikedProject;

public class UpdateLikedProjectHandler : IRequestHandler<UpdateLikedProjectCommand, LikedProjectResponse>
{
    private readonly ILikedProjectRepository _repository;

    public UpdateLikedProjectHandler(ILikedProjectRepository repository)
    {
        _repository = repository;
    }

    public async Task<LikedProjectResponse> Handle(UpdateLikedProjectCommand request, CancellationToken cancellationToken)
    {
        var likedProject = await _repository.GetByIDAsync(request.Id);

        if (likedProject == null)
            throw new NotFoundException($"Liked project with ID {request.Id} not found.");

        likedProject.ProjectId = request.ProjectId;

        _ = _repository.Update(likedProject);
        await _repository.SaveAsync();

        return new LikedProjectResponse
        {
            Id = likedProject.Id,
            UserId = likedProject.UserId,
            ProjectId = likedProject.ProjectId,
            LikedAt = likedProject.LikedAt
        };
    }
}