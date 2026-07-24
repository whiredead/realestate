namespace ProjectAPI.Api.Application.Projects.LikedProjects.RemoveLikedProject;

using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Domain.Projects.Interfaces;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Users.Interfaces;

public class RemoveLikedProjectHandler
    : IRequestHandler<RemoveLikedProjectCommand, RemoveLikedProjectResponse>
{
    private readonly ILikedProjectRepository _likes;
    private readonly IProjectRepository _projects;
    private readonly IImmeubleRepository _immeubles;
    private readonly ILeadRepository _leads;
    private readonly IPerformanceIndicatorRepository _perf;

    public RemoveLikedProjectHandler(
        ILikedProjectRepository likes,
        IProjectRepository projects,
        IImmeubleRepository immeubles,
        ILeadRepository leads,
        IPerformanceIndicatorRepository perf)
    {
        _likes = likes;
        _projects = projects;
        _immeubles = immeubles;
        _leads = leads;
        _perf = perf;
    }

    public async Task<RemoveLikedProjectResponse> Handle(
        RemoveLikedProjectCommand request,
        CancellationToken cancellationToken)
    {
        // 1) find the existing like record
        var existing = (await _likes.Find(lp =>
            lp.UserId == request.UserId &&
            lp.ProjectId == request.ProjectId))
            .FirstOrDefault();

        if (existing is null)
            throw new NotFoundException(
                $"User {request.UserId} has not liked project {request.ProjectId}");

        // IMPORTANT: Delete/Update save internally on a shared DbContext, so every
        // read is performed BEFORE any write. Interleaving them raised
        // "A second operation was started on this context instance".

        // --- reads ---
        var proj = await _projects.GetByIDAsync(request.ProjectId)
                  ?? throw new NotFoundException($"Project {request.ProjectId} not found");

        var immeubles = (await _immeubles.Find(i => i.ProjectId == request.ProjectId)).ToList();

        var leadsToDelete = new List<Lead>();
        var perfDecrements = new Dictionary<string, PerformanceIndicator>();

        foreach (var im in immeubles)
        {
            var leads = (await _leads.Find(l =>
                l.ProjectId == request.ProjectId &&
                l.UserId == request.UserId &&
                l.AgentId == im.AgentId)).ToList();

            if (leads.Count == 0) continue;
            leadsToDelete.AddRange(leads);

            if (string.IsNullOrWhiteSpace(im.AgentId) || perfDecrements.ContainsKey(im.AgentId))
                continue;

            var pi = (await _perf.Find(p => p.AgentId == im.AgentId)).FirstOrDefault();
            if (pi is not null)
            {
                perfDecrements[im.AgentId] = pi;
            }
        }

        // --- writes ---
        _likes.Delete(existing);

        proj.NumberLikes = Math.Max(0, proj.NumberLikes - 1);
        await _projects.Update(proj);

        foreach (var lead in leadsToDelete)
        {
            _leads.Delete(lead);
        }

        foreach (var pi in perfDecrements.Values)
        {
            if (pi.LeadsGenerated > 0)
            {
                pi.LeadsGenerated -= 1;
                await _perf.Update(pi);
            }
        }

        await _likes.SaveAsync();
        await _projects.SaveAsync();
        await _leads.SaveAsync();
        await _perf.SaveAsync();

        return new RemoveLikedProjectResponse
        {
            Success = true,
            Message = "Project successfully un-liked."
        };
    }
}

