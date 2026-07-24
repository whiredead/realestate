using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.EspacesTempsReel.CreateEspaceTempsReel
{
    public class CreateEspaceTempsReelHandler : IRequestHandler<CreateEspaceTempsReelCommand, CreateEspaceTempsReelResponse>
    {
        private readonly IEspaceTempsReelRepository _repository;
        private readonly IProjectRepository _projectRepository;

        public CreateEspaceTempsReelHandler(IEspaceTempsReelRepository repository, IProjectRepository projectRepository)
        {
            _repository = repository;
            _projectRepository = projectRepository;
        }

        public async Task<CreateEspaceTempsReelResponse> Handle(CreateEspaceTempsReelCommand request, CancellationToken cancellationToken)
        {
            // Validate project
            var project = await _projectRepository.GetByIDAsync(request.ProjectId);
            if (project == null)
            {
                throw new NotFoundException($"Project with ID {request.ProjectId} not found.");
            }

            var entity = new EspaceTempsReel
            {
                Id = Guid.NewGuid(),
                ProjectId = request.ProjectId,
                VideoLink = request.VideoLink,
                InsertedAt = request.InsertedAt ?? DateTime.Now
            };

            await _repository.InsertAsync(entity);
            await _repository.SaveAsync();

            return new CreateEspaceTempsReelResponse
            {
                Id = entity.Id,
                Message = "Video entry created successfully."
            };
        }
    }
}
