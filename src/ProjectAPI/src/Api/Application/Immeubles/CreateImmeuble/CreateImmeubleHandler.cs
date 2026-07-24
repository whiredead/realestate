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

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateImmeubleHandler"/> class.
        /// </summary>
        /// <param name="repository">The repository to handle immeuble data operations.</param>
        /// <param name="projectRepository">The repository to handle project data operations.</param>

        public CreateImmeubleHandler(IImmeubleRepository repository, IProjectRepository projectRepository, IImmeubleTrackingRepository immeubleTrackingRepository)
        {
            _repository = repository;
            _projectRepository = projectRepository;
            _immeubleTrackingRepository = immeubleTrackingRepository;
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

            var immeuble = new Immeuble
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                ProjectId = request.ProjectId,
                Location = request.Location,
                Type = request.Type,
                Images = request.Images,
                MinPrice = request.MinPrice,
                MaxPrice = request.MaxPrice,
                Description = request.Description,
                Longitude = request.Longitude,
                Latitude = request.Latitude,
                Status = "ComingSoon",
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
                StatusUpdate = "ComingSoon",
                DateUpdated = DateTime.Now
            };

            await _immeubleTrackingRepository.InsertAsync(immeubleTracking);
            await _immeubleTrackingRepository.SaveAsync();

            return project.Id;
        }
    }
}