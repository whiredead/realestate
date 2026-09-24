using ProjectAPI.Api.Application.Common.Media;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Api.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ProjectAPI.Api.Application.Projects.UpdateProjects;

public class UpdateProjectHandler : IRequestHandler<UpdateProjectCommand, ProjectResponse>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IQuartierRepository _quartierRepository;
    private readonly ProjectScopeService _projectScope;
    private readonly MediaUrlPolicy _media;
    private readonly Infrastructure.Context.ApplicationDbContext _db;

    public UpdateProjectHandler(
        IProjectRepository projectRepository,
        IQuartierRepository quartierRepository,
        ProjectScopeService projectScope,
        MediaUrlPolicy media,
        Infrastructure.Context.ApplicationDbContext db)
    {
        _projectRepository = projectRepository;
        _quartierRepository = quartierRepository;
        _projectScope = projectScope;
        _media = media;
        _db = db;
    }

    public async Task<ProjectResponse> Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIDAsync(request.Id)
            ?? throw new NotFoundException($"Project with ID {request.Id} not found.");

        await _projectScope.EnsureProjectAccessAsync(request.Id, cancellationToken);

        // §7.2 — only what the client is ADDING is checked. An edit form resends
        // the images it was given, so validating the whole list would make a
        // project whose catalogue predates this policy unsavable for a reason
        // that has nothing to do with the edit being made (§9 — existing data
        // is preserved, not retroactively rejected).
        _media.EnsureImageUrls(request.Images, "Images", project.Images);
        if (request.Module3DLink is not null && request.Module3DLink != project.Module3DLink)
        {
            _media.Ensure3DLink(request.Module3DLink, "Module3DLink");
        }
        _media.EnsureImageUrl(request.QuartierImages, "QuartierImages");
        await ProjectTypeBienLinks.EnsureQuartierExistsAsync(_db, request.QuartierId, cancellationToken);

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

        // Statut (StatusReferenceCode) and phase (StatusGlobal) are independent:
        // the business-facing statut is a free label an admin picks from the
        // referential, and no longer forces a phase transition just because its
        // BusinessPhase happens to point at one — phase only ever advances
        // through the construction panel's dedicated complete/finalize actions
        // (ConstructionPanel.tsx -> constructionApi.completeProject/
        // finalizeProject), which is the one-way SUR_PLAN -> EN_LIVRAISON ->
        // FINALISE guard this comment used to describe. A project already
        // FINALISE never reaches this point either way: ProjectReadOnlyGuard
        // refuses every write on it (409 PROJECT_READ_ONLY).
        if (request.StatusReferenceCode != null)
        {
            var selectedStatusCode = request.StatusReferenceCode.Trim().ToUpperInvariant();
            var status = await _db.ProjectStatusReferences.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == selectedStatusCode && (x.IsActive || x.Code == project.StatusReferenceCode), cancellationToken);
            if (status is null)
            {
                throw new ProjectAPI.Api.Application.Common.Exceptions.ValidationException(new[]
                {
                    new FluentValidation.Results.ValidationFailure(nameof(request.StatusReferenceCode), "Le statut sélectionné n'est pas disponible.")
                });
            }
            project.StatusReferenceCode = status.Code;
        }

        project.OverAllProgress = request.OverallProgress ?? project.OverAllProgress;

        // §8 — takes effect for sales opened from now on; sales already created
        // carry their own frozen copy and are unaffected.
        project.WarrantyMonths = request.WarrantyMonths ?? project.WarrantyMonths;

        // BaseRepository.Update already persists (it awaits SaveAsync internally).
        // The previous code fired Update() un-awaited AND then called SaveAsync()
        // again, racing two operations on the same DbContext → 500 "a second
        // operation was started on this context". Await the single save.
        await _projectRepository.Update(project);

        if (request.TypeBienIds is not null)
        {
            await ProjectTypeBienLinks.SyncAsync(_db, project.Id, request.TypeBienIds, cancellationToken);
        }

        return new ProjectResponse
        {
            Id = project.Id,
            Name = project.Name,
            Location = project.Location,
            Address = project.Address,
            Description = project.Description,
            Images = project.Images,
            Module3DLink = project.Module3DLink,
            QuartierId = project.QuartierId,
            StatusGlobal = project.StatusGlobal,
            StatusReferenceCode = project.StatusReferenceCode,
            WarrantyMonths = project.WarrantyMonths,
        };
    }
}
