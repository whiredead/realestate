using ProjectAPI.Api.Application.Common.Models;

namespace ProjectAPI.Api.Application.Projects.GetAllProjects;

public class GetAllProjectsQuery : IRequest<PaginatedResponse<ProjectResponse>>
{
    
    /// <summary>
    /// The name of the project to filter by.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>One project by id (the detail page). No other by-id read returns this full shape.</summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// The location of the project to filter by.
    /// </summary>
    public string? Location { get; set; }
    public string? UserId { get; set; }

    /// <summary>
    /// The adress to filter by.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// The status to filter by (e.g., "CommingSoon", "Available", "Deleted").
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// The maximum sellable surface area to filter by.
    /// </summary>
    public int? MaxSellableSurfaceRange { get; set; }

    /// <summary>
    /// The current page number.
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// The number of items per page.
    /// </summary>
    public int PageSize { get; set; } = 10;
}
