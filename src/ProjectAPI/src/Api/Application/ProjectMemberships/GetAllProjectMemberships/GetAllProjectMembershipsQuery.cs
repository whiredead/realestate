using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.ProjectMemberships.GetProjectMembershipById;

namespace ProjectAPI.Api.Application.ProjectMemberships.GetAllProjectMemberships;

public class GetAllProjectMembershipsQuery : IRequest<PaginatedResponse<GetProjectMembershipByIdResponse>>
{
    public Guid? ProjectId { get; set; }
    public string? UserId { get; set; }
    public string? RoleCode { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
