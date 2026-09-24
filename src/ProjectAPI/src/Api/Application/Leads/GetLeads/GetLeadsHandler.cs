using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Leads.GetLeads;

/// <summary>
/// Lead has had a repository and DI wiring since AddLikedProjectHandler
/// started writing rows on every "favorite a project" action, but no
/// controller or query ever read them back — the same missing-UI pattern
/// found elsewhere on this project (an entity fully wired on the write side,
/// invisible everywhere else). This is that read path, project-scoped like
/// every other admin list (§6.4).
/// </summary>
public class GetLeadsHandler : IRequestHandler<GetLeadsQuery, PaginatedResponse<LeadResponse>>
{
    private readonly ApplicationDbContext _context;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;

    public GetLeadsHandler(ApplicationDbContext context, ProjectScopeService projectScope, ICurrentUser currentUser)
    {
        _context = context;
        _projectScope = projectScope;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResponse<LeadResponse>> Handle(GetLeadsQuery request, CancellationToken cancellationToken)
    {
        var scopedProjectIds = await _projectScope.GetScopedProjectIdsAsync(cancellationToken);

        // A SALES_AGENT sees only their own leads — same convention as
        // GetAppointmentsHandler/GetReservationsHandler.
        // A SALES_AGENT sees every lead of the projects assigned to them; AgentId is only an optional filter.
        var effectiveAgentId = request.AgentId;

        var query = _context.Set<Lead>()
            .Where(l => scopedProjectIds == null || scopedProjectIds.Contains(l.ProjectId))
            .Where(l => !request.ProjectId.HasValue || l.ProjectId == request.ProjectId.Value)
            .Where(l => string.IsNullOrEmpty(effectiveAgentId) || l.AgentId == effectiveAgentId);

        var totalItems = await query.CountAsync(cancellationToken);

        var leads = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var projectIds = leads.Select(l => l.ProjectId).Distinct().ToList();
        var projectNames = await _context.Projects
            .Where(p => projectIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var userIds = leads.Select(l => l.UserId).Concat(leads.Select(l => l.AgentId))
            .Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        var userNames = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}", cancellationToken);

        var data = leads.Select(l => new LeadResponse
        {
            Id = l.Id,
            ProjectId = l.ProjectId,
            ProjectName = projectNames.GetValueOrDefault(l.ProjectId, ""),
            UserId = l.UserId,
            UserFullName = l.UserId != null ? userNames.GetValueOrDefault(l.UserId) : null,
            AgentId = l.AgentId,
            AgentFullName = l.AgentId != null ? userNames.GetValueOrDefault(l.AgentId) : null,
            Description = l.Description,
            CreatedAt = l.CreatedAt,
        }).ToList();

        return new PaginatedResponse<LeadResponse>(data, request.PageNumber, request.PageSize, totalItems);
    }
}
