using ProjectAPI.Domain.Construction.Entities;
﻿using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.Immeubles.CreateImmeuble
{
    /// <summary>
    /// Handler for creating a new project.
    /// </summary>
    public class CreateImmeubleHandler : IRequestHandler<CreateImmeubleCommand, Guid>
    {
        private readonly IImmeubleRepository _repository;
        private readonly IProjectRepository _projectRepository;
        private readonly IImmeubleTrackingRepository _immeubleTrackingRepository;
        private readonly ProjectScopeService _projectScope;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateImmeubleHandler"/> class.
        /// </summary>
        /// <param name="repository">The repository to handle immeuble data operations.</param>
        /// <param name="projectRepository">The repository to handle project data operations.</param>
        /// <param name="immeubleTrackingRepository">The repository to handle immeuble tracking operations.</param>
        /// <param name="projectScope">Enforces §6.4: a building may only be created within the caller's own assigned project.</param>
        public CreateImmeubleHandler(
            IImmeubleRepository repository,
            IProjectRepository projectRepository,
            IImmeubleTrackingRepository immeubleTrackingRepository,
            ProjectScopeService projectScope)
        {
            _repository = repository;
            _projectRepository = projectRepository;
            _immeubleTrackingRepository = immeubleTrackingRepository;
            _projectScope = projectScope;
        }

        /// <summary>
        /// Handles the creation of a new project.
        /// </summary>
        /// <param name="request">The command containing project details.</param>
        /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
        /// <returns>The unique identifier of the created project.</returns>
        public async Task<Guid> Handle(CreateImmeubleCommand request, CancellationToken cancellationToken)
        {

            // Check if the project exists
            var project = await _projectRepository.GetByIDAsync(request.ProjectId) ?? throw new Exception($"Project with ID {request.ProjectId} does not exist.");

            await _projectScope.EnsureProjectAccessAsync(request.ProjectId, cancellationToken);

            var immeuble = new Immeuble
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                ProjectId = request.ProjectId,
                Location = request.Location,
                // Type and price range are managed on TypeBien, not on a new
                // building. Existing columns stay populated for compatibility
                // with legacy listings until their data is migrated.
                Type = string.Empty,
                Images = request.Images,
                Description = request.Description,
                // A new building starts with its project's status (SUR_PLAN / EN_LIVRAISON / FINALISE).
                Status = ProjectStatusCodes.Normalize(project.StatusGlobal),
                NumberOfUnits = request.NumberOfUnits,
                MinSellableSurfaceRange = request.MinSellableSurfaceRange,
                MaxSellableSurfaceRange = request.MaxSellableSurfaceRange,
                Module3DLink = request.Module3DLink,
                NumberOfAvailableUnites = request.NumberOfAvailableUnites,
                NumberOfSoldUnites = request.NumberOfSoldUnites,
                SellsPercentage = request.SellsPercentage
            };

            await _repository.InsertAsync(immeuble);
            await _repository.SaveAsync();

            var immeubleTracking = new ImmeubleTracking
            {
                Id = Guid.NewGuid(),
                ImmeubleId = immeuble.Id,
                StatusUpdate = immeuble.Status,
                DateUpdated = DateTime.Now
            };

            await _immeubleTrackingRepository.InsertAsync(immeubleTracking);
            await _immeubleTrackingRepository.SaveAsync();

            return immeuble.Id;
        }
    }
}
