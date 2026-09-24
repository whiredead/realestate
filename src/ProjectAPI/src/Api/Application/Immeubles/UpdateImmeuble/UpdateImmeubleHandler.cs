using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Immeubles.Interfaces;

namespace ProjectAPI.Api.Application.Immeubles.UpdateImmeuble
{
    /// <summary>
    /// Handler for the <see cref="UpdateImmeubleCommand"/>.
    /// </summary>
    public class UpdateImmeubleHandler : IRequestHandler<UpdateImmeubleCommand, ImmeubleResponse>
    {
        private readonly IImmeubleRepository _repository;
        private readonly IImmeubleTrackingRepository _trackingRepository;
        private readonly ProjectScopeService _projectScope;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateImmeubleHandler"/> class.
        /// </summary>
        /// <param name="repository">The immeuble repository.</param>
        /// <param name="trackingRepository">The tracking repository.</param>
        /// <param name="projectScope">Enforces §6.4: a building may only be edited by staff assigned to its project.</param>
        public UpdateImmeubleHandler(IImmeubleRepository repository, IImmeubleTrackingRepository trackingRepository, ProjectScopeService projectScope)
        {
            _repository = repository;
            _trackingRepository = trackingRepository;
            _projectScope = projectScope;
        }

        /// <summary>
        /// Handles the request to update an immeuble.
        /// </summary>
        /// <param name="request">The request containing immeuble update details.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response containing the updated immeuble details.</returns>
        public async Task<ImmeubleResponse> Handle(UpdateImmeubleCommand request, CancellationToken cancellationToken)
        {
            var immeuble = await _repository.GetByIDAsync(request.Id)
                           ?? throw new NotFoundException($"Immeuble with ID {request.Id} not found.");

            // Immeuble.ProjectId is the real FK to Project (unlike Unit.ProjectId,
            // which is actually the FK to Immeuble) — no indirection needed here.
            await _projectScope.EnsureProjectAccessAsync(immeuble.ProjectId, cancellationToken);

            // Track the original status
            var originalStatus = immeuble.Status;

            // Dictionary to manage field updates dynamically
            var updateActions = new Dictionary<Func<bool>, Action>
            {
                { () => !string.IsNullOrWhiteSpace(request.Name), () => immeuble.Name = request.Name },
                { () => !string.IsNullOrWhiteSpace(request.Location), () => immeuble.Location = request.Location },
                { () => !string.IsNullOrWhiteSpace(request.Type), () => immeuble.Type = request.Type },
                { () => !string.IsNullOrWhiteSpace(request.Status), () => immeuble.Status = ProjectStatusCodes.Normalize(request.Status) },
                // An empty string is a deliberate update: it removes the last
                // photo from a building. Previously this ignored an empty
                // value, making the final image impossible to delete.
                { () => request.Images is not null, () => immeuble.Images = request.Images },
                { () => !string.IsNullOrWhiteSpace(request.Description), () => immeuble.Description = request.Description },
                { () => request.MaxSellableSurfaceRange > 0, () => immeuble.MaxSellableSurfaceRange = request.MaxSellableSurfaceRange },
                { () => request.MinSellableSurfaceRange > 0, () => immeuble.MinSellableSurfaceRange = request.MinSellableSurfaceRange }
            };

            // Apply updates only if conditions are met
            foreach (var updateAction in updateActions)
            {
                if (updateAction.Key.Invoke())
                {
                    updateAction.Value.Invoke();
                }
            }

            // If the status has changed, add an entry to ImmeubleTracking
            if (originalStatus != immeuble.Status)
            {
                var trackingEntry = new ImmeubleTracking
                {
                    Id = Guid.NewGuid(),
                    ImmeubleId = immeuble.Id,
                    StatusUpdate = immeuble.Status,
                    DateUpdated = DateTime.Now
                };

                await _trackingRepository.InsertAsync(trackingEntry);
                await _trackingRepository.SaveAsync();
            }

            await _repository.Update(immeuble);
            await _repository.SaveAsync();

            return new ImmeubleResponse
            {
                Id = immeuble.Id,
                Name = immeuble.Name,
                Location = immeuble.Location,
                Type = immeuble.Type,
                Status = ProjectStatusCodes.Normalize(immeuble.Status),
                Images = immeuble.Images.Split(',').ToList(),
                Description = immeuble.Description
            };
        }
    }
}
