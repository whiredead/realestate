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
            // Idempotent: liking an already-liked project returns the existing
            // favourite. It used to insert a second row, count the like twice and
            // duplicate every lead and agent increment — which the un-favourite
            // could never fully walk back.
            var already = (await _repository.Find(lp => lp.UserId == request.UserId && lp.ProjectId == request.ProjectId)).FirstOrDefault();
            if (already is not null)
            {
                return new LikedProjectResponse
                {
                    Id = already.Id,
                    UserId = already.UserId,
                    ProjectId = already.ProjectId,
                    LikedAt = already.LikedAt
                };
            }

            // Add liked project
            var likedProject = new LikedProject
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                ProjectId = request.ProjectId,
                LikedAt = DateTime.UtcNow
            };

            // IMPORTANT: InsertAsync/Update save internally on a shared DbContext.
            // All reads are therefore performed FIRST; writes happen afterwards.
            // Interleaving them raised "A second operation was started on this
            // context instance before a previous operation completed".

            // --- reads ---
            var project = await _projectRepository.GetByIDAsync(request.ProjectId)
                ?? throw new Common.Exceptions.NotFoundException($"Project {request.ProjectId} not found.");
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

            project.NumberLikes++;
            await _projectRepository.Update(project);

            // One lead per distinct agent (a shared null counts as one "no
            // agent yet" lead too) — not one per immeuble. Two immeubles on
            // the same agent used to insert two Lead rows for the same
            // like; RemoveLikedProjectHandler's un-favourite then queried
            // and tried to delete that pair twice each, and the second
            // delete of an already-gone row read as a stale write (409
            // RESOURCE_VERSION_CONFLICT) on every un-favourite of such a
            // project.
            foreach (var agentId in immeubles.Select(i => i.AgentId).Distinct())
            {
                var lead = new Lead
                {
                    Id = Guid.NewGuid(),
                    ProjectId = request.ProjectId,
                    UserId = request.UserId,
                    AgentId = agentId,
                    CreatedAt = DateTime.UtcNow
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
