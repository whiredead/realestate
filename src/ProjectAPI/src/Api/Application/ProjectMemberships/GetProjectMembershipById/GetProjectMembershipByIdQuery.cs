namespace ProjectAPI.Api.Application.ProjectMemberships.GetProjectMembershipById;

public class GetProjectMembershipByIdQuery : IRequest<GetProjectMembershipByIdResponse>
{
    public Guid Id { get; set; }
}
