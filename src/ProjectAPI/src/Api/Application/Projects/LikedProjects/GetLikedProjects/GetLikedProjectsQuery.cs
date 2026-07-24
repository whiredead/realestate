using ProjectAPI.Api.Application.Common.Models;

namespace ProjectAPI.Api.Application.Projects.LikedProjects.GetLikedProjects;

public class GetLikedProjectsQuery : IRequest<PaginatedResponse<LikedProjectResponse>>
{
    public string? UserId { get; set; }
    public string? ProjectId { get; set; }

    /// <summary>                                       
    /// The current page number.                                     
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// The number of items per page.
    /// </summary>
    public int PageSize { get; set; } = 10;
}