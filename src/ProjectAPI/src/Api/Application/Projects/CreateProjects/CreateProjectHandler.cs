using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.Projects.CreateProjects;

public class CreateProjectHandler : IRequestHandler<CreateProjectCommand, CreateProjectResponse>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IQuartierRepository _quartierRepository;

    public CreateProjectHandler(IProjectRepository projectRepository, IQuartierRepository quartierRepository)
    {
        _projectRepository = projectRepository;
        _quartierRepository = quartierRepository;
    }

    public async Task<CreateProjectResponse> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        // Step 1: Resolve or create Quartier
        Guid? quartierId = null;
        if (request.QuartierId.HasValue)
        {
            // Use existing quartier
            quartierId = request.QuartierId;
        }
        else if (!string.IsNullOrEmpty(request.QuartierName))
        {
            // Create new quartier
            var newQuartier = new Quartier
            {
                Id = Guid.NewGuid(),
                Name = request.QuartierName!,
                Description = request.QuartierDescription ?? string.Empty,
                Images = request.QuartierImages ?? string.Empty
            };

            await _quartierRepository.InsertAsync(newQuartier);
            await _quartierRepository.SaveAsync();
            quartierId = newQuartier.Id;
        }

        // Step 2: Create a new Project entity
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Location = request.Location,
            Address = request.Address,
            Description = request.Description ?? string.Empty,
            // Module3DLink is a NOT NULL column; a client that omits it (the admin
            // "Nouveau projet" form does) must not crash the insert. Coalesce.
            Module3DLink = request.Module3DLink ?? string.Empty,
            Images = request.Images,
            QuartierId = quartierId,
            Type = request.Type ?? "Livraison immédiate",
            // §3 / FR-CMS-001 — a project is created in DRAFT. Normalised so the
            // column only ever holds canonical codes, never the legacy (and
            // misspelled) "CommingSoon" spellings.
            StatusGlobal = ProjectStatusCodes.Normalize(request.StatusGlobal ?? ProjectStatusCodes.Draft)
        };

        // Step 3: Save to repository
        await _projectRepository.InsertAsync(project);
        await _projectRepository.SaveAsync();

        // Step 4: Build response
        return new CreateProjectResponse
        {
            Id = project.Id,
            Message = "Project created successfully."
        };
    }
}