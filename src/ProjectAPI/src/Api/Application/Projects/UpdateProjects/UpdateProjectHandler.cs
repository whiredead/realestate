using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Api.Application.Common.Exceptions;

namespace ProjectAPI.Api.Application.Projects.UpdateProjects;

public class UpdateProjectHandler : IRequestHandler<UpdateProjectCommand, ProjectResponse>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IQuartierRepository _quartierRepository;
    private readonly ProjectScopeService _projectScope;

    public UpdateProjectHandler(
        IProjectRepository projectRepository,
        IQuartierRepository quartierRepository,
        ProjectScopeService projectScope)
    {
        _projectRepository = projectRepository;
        _quartierRepository = quartierRepository;
        _projectScope = projectScope;
    }

    public async Task<ProjectResponse> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIDAsync(request.Id)
            ?? throw new NotFoundException($"Project with ID {request.Id} not found.");

        await _projectScope.EnsureProjectAccessAsync(request.Id, cancellationToken);

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

        // §3 / §15.3 — COMPLETED is a gate, not a field. It unlocks final-visit
        // requests, so it may only be reached through CompleteProjectCommand,
        // which verifies 100% weighted progress, an explicit confirmation and a
        // real end date, and records the decision. Letting a generic PUT write it
        // would reopen the gate to anyone who can edit a project.
        if (request.StatusGlobal != null)
        {
            var target = ProjectStatusCodes.Normalize(request.StatusGlobal);
            var current = ProjectStatusCodes.Normalize(project.StatusGlobal);

            if (target == ProjectStatusCodes.Completed && current != ProjectStatusCodes.Completed)
            {
                throw new BusinessRuleException(
                    BusinessErrorCodes.InvalidStatusTransition,
                    "Un projet ne peut être marqué COMPLETED que via la commande de finalisation " +
                    "(POST /api/construction/projects/{id}/complete), qui vérifie l'avancement à 100%.",
                    StatusCodes.Status409Conflict);
            }

            project.StatusGlobal = target;
        }

        project.OverAllProgress = request.OverallProgress ?? project.OverAllProgress;

        // BaseRepository.Update already persists (it awaits SaveAsync internally).
        // The previous code fired Update() un-awaited AND then called SaveAsync()
        // again, racing two operations on the same DbContext → 500 "a second
        // operation was started on this context". Await the single save.
        await _projectRepository.Update(project);

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
