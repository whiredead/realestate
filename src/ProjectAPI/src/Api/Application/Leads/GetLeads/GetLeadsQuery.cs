using ProjectAPI.Api.Application.Common.Models;

namespace ProjectAPI.Api.Application.Leads.GetLeads;

/// <summary>
/// Query to list leads (a project favorited by a user — see AddLikedProjectHandler,
/// the only writer of Lead today) with optional filters.
/// </summary>
public class GetLeadsQuery : IRequest<PaginatedResponse<LeadResponse>>
{
    public Guid? ProjectId { get; set; }
    public string? AgentId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
