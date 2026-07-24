using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Users.Interfaces;

namespace ProjectAPI.Api.Application.Projects.LikedProjects.AddLikedProject
{
    public class AddLikedProjectHandler : IRequestHandler<AddLikedProjectCommand, LikedProjectResponse>
    {
        private readonly ILikedProjectRepository _repository;
        private readonly IImmeubleRepository _immeubleRepository;
        private readonly IPerformanceIndicatorRepository _performanceIndicatorRepository;
        private readonly ILeadRepository _leadRepository;
        private readonly IProjectRepository _projectRepository;

        public AddLikedProjectHandler(
            ILikedProjectRepository repository,
            IImmeubleRepository immeubleRepository,
            IPerformanceIndicatorRepository performanceIndicatorRepository,
            ILeadRepository leadRepository,
            IProjectRepository projectRepository)
        {
            _repository = repository;
            _immeubleRepository = immeubleRepository;
            _performanceIndicatorRepository = performanceIndicatorRepository;
            _leadRepository = leadRepository;
            _projectRepository = projectRepository;
        }

        public async Task<LikedProjectResponse> Handle(AddLikedProjectCommand request, CancellationToken cancellationToken)
        {
            // Add liked project
            var likedProject = new LikedProject
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                ProjectId = request.ProjectId,
                LikedAt = DateTime.Now
            };

            // IMPORTANT: InsertAsync/Update save internally on a shared DbContext.
            // All reads are therefore performed FIRST; writes happen afterwards.
            // Interleaving them raised "A second operation was started on this
            // context instance before a previous operation completed".

            // --- reads ---
            var project = await _projectRepository.GetByIDAsync(request.ProjectId);
            var immeubles = (await _immeubleRepository.Find(i => i.ProjectId == request.ProjectId)).ToList();

            var agentIds = immeubles
                .Select(i => i.AgentId)
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct()
                .ToList();

            var indicatorsByAgent = new Dictionary<string, PerformanceIndicator>();
            foreach (var agentId in agentIds)
            {
                var found = await _performanceIndicatorRepository.Find(i => i.AgentId == agentId);
                var indicator = found.FirstOrDefault();
                if (indicator != null)
                {
                    indicatorsByAgent[agentId!] = indicator;
                }
            }

            // --- writes ---
            await _repository.InsertAsync(likedProject);

            if (project != null)
            {
                project.NumberLikes++;
                await _projectRepository.Update(project);
            }

            foreach (var immeuble in immeubles)
            {
                var lead = new Lead
                {
                    Id = Guid.NewGuid(),
                    ProjectId = request.ProjectId,
                    UserId = request.UserId,
                    AgentId = immeuble.AgentId,
                    CreatedAt = DateTime.Now
                };

                await _leadRepository.InsertAsync(lead);
            }

            foreach (var indicator in indicatorsByAgent.Values)
            {
                indicator.IncrementLeadsGenerated();
                await _performanceIndicatorRepository.Update(indicator);
            }

            return new LikedProjectResponse
            {
                Id = likedProject.Id,
                UserId = likedProject.UserId,
                ProjectId = likedProject.ProjectId,
                LikedAt = likedProject.LikedAt
            };
        }
    }
}
