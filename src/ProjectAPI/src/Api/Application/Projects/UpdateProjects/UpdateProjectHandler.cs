using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Api.Application.Common.Exceptions;

namespace ProjectAPI.Api.Application.Projects.UpdateProjects;

public class UpdateProjectHandler : IRequestHandler<UpdateProjectCommand, ProjectResponse>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IQuartierRepository _quartierRepository;

    public UpdateProjectHandler(IProjectRepository projectRepository, IQuartierRepository quartierRepository)
    {
        _projectRepository = projectRepository;
        _quartierRepository = quartierRepository;
    }

    public async Task<ProjectResponse> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIDAsync(request.Id)
            ?? throw new NotFoundException($"Project with ID {request.Id} not found.");

        // Update Quartier if needed
        if (request.QuartierId.HasValue)
        {
            project.QuartierId = request.QuartierId;
        }
        else if (!string.IsNullOrEmpty(request.QuartierName))
        {
            var newQuartier = new Quartier
            {
                Id = Guid.NewGuid(),
                Name = request.QuartierName!,
                Description = request.QuartierDescription ?? "",
                Images = request.QuartierImages ?? ""
            };

            await _quartierRepository.InsertAsync(newQuartier);
            await _quartierRepository.SaveAsync();
            project.QuartierId = newQuartier.Id;
        }

        // Update project fields
        project.Name = request.Name ?? project.Name;
        project.Location = request.Location ?? project.Location;
        project.Address = request.Address ?? project.Address;
        project.Description = request.Description ?? project.Description;
        project.Module3DLink = request.Module3DLink ?? project.Module3DLink;
        project.Images = request.Images ?? project.Images;
        project.Type = request.Type ?? project.Type;
        project.StatusGlobal = request.StatusGlobal ?? project.StatusGlobal;
        project.OverAllProgress = request.OverallProgress ?? project.OverAllProgress;

        _projectRepository.Update(project);
        await _projectRepository.SaveAsync();

        return new ProjectResponse
        {
            Id = project.Id,
            Name = project.Name,
            Location = project.Location,
            Address = project.Address,
            Description = project.Description,
            Images = project.Images,
            Module3DLink = project.Module3DLink,
        };
    }
}
