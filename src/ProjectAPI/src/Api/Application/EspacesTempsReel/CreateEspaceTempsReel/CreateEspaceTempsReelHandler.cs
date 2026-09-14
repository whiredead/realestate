using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Projects.Interfaces;

namespace ProjectAPI.Api.Application.EspacesTempsReel.CreateEspaceTempsReel
{
    public class CreateEspaceTempsReelHandler : IRequestHandler<CreateEspaceTempsReelCommand, CreateEspaceTempsReelResponse>
    {
        private readonly IEspaceTempsReelRepository _repository;
        private readonly IProjectRepository _projectRepository;
        private readonly ProjectScopeService _projectScope;
        private readonly Common.Media.MediaUrlPolicy _media;

        public CreateEspaceTempsReelHandler(
            IEspaceTempsReelRepository repository,
            IProjectRepository projectRepository,
            ProjectScopeService projectScope,
            Common.Media.MediaUrlPolicy media)
        {
            _repository = repository;
            _projectRepository = projectRepository;
            _projectScope = projectScope;
            _media = media;
        }

        public async Task<CreateEspaceTempsReelResponse> Handle(CreateEspaceTempsReelCommand request, CancellationToken cancellationToken)
        {
            // Validate project
            var project = await _projectRepository.GetByIDAsync(request.ProjectId);
            if (project == null)
            {
                throw new NotFoundException($"Project with ID {request.ProjectId} not found.");
            }

            // §6.4 — a PROJECT_ADMIN with no membership on this project must
            // not publish a video link onto its public page.
            await _projectScope.EnsureProjectAccessAsync(request.ProjectId, cancellationToken);

            // §7.2 — the link is embedded on the project's public page, so it is
            // restricted to the configured video hosts (see MediaUrlPolicy).
            _media.EnsureVideoUrl(request.VideoLink, "VideoLink");

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
