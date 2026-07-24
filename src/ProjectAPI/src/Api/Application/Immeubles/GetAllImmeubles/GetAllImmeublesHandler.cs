using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Domain.Immeubles.Interfaces;

namespace ProjectAPI.Api.Application.Immeubles.GetAllImmeubles;

/// <summary>
/// Handler for getting all projects.
/// </summary>
public class GetAllImmeublesHandler : IRequestHandler<GetAllImmeublesQuery, PaginatedResponse<ImmeubleResponse>>
{
    private readonly IImmeubleRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetAllImmeublesHandler"/> class.
    /// </summary>
    /// <param name="repository">The project repository to access project data.</param>
    public GetAllImmeublesHandler(IImmeubleRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Handles the query to get all projects.
    /// </summary>
    /// <param name="request">The query containing pagination and filtering details.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A paginated response containing the projects.</returns>
    public async Task<PaginatedResponse<ImmeubleResponse>> Handle(GetAllImmeublesQuery request, CancellationToken cancellationToken)
    {
        var projects = await _repository.Find(
            p => (string.IsNullOrEmpty(request.Name) || p.Name.Contains(request.Name)) &&
                 (string.IsNullOrEmpty(request.Location) || p.Location!.Contains(request.Location)) &&
                 (!request.ProjectId.HasValue || p.ProjectId == request.ProjectId) &&
                 (request.MinPrice == null || p.MinPrice >= request.MinPrice) &&
                 (request.MaxPrice == null || p.MaxPrice <= request.MaxPrice) &&
                 (request.Type == null || p.Type == request.Type.ToString()) &&
                 (request.MinSellableSurfaceRange == null || p.MinSellableSurfaceRange >= request.MinSellableSurfaceRange) &&
                 (request.MaxSellableSurfaceRange == null || p.MaxSellableSurfaceRange <= request.MaxSellableSurfaceRange),p => p.PlanInterieurs
        );


        var totalItems = projects.Count();
        var paginatedData = projects
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new ImmeubleResponse
            {
                Id = p.Id,
                ProjectId = p.ProjectId,
                AgentId = p.AgentId,
                Name = p.Name,
                Location = p.Location,
                Type = p.Type,
                MinPrice = p.MinPrice,
                MaxPrice = p.MaxPrice,
                Status = Enum.Parse<ProjectStatus>(p.Status),
                Images = [.. p.Images.Split(',')],
                Description = p.Description,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                NumberOfUnits = p.NumberOfUnits,
                MaxSellableSurfaceRange = p.MaxSellableSurfaceRange,
                MinSellableSurfaceRange = p.MinSellableSurfaceRange,
                Module3DLink = p.Module3DLink,
                NumberOfAvailableUnites = p.NumberOfUnits,
                NumberOfSoldUnites = p.NumberOfSoldUnites,
                SellsPercentage = p.SellsPercentage,
                PlanInerieurs = p.PlanInterieurs.Select(pi => new PlanInerieurResponse
                {
                    Id = pi.Id,
                    ImmeubleId = pi.ImmeubleId,
                    PhotoLinks = pi.PhotoLinks
                }).ToList()
            });

        return new PaginatedResponse<ImmeubleResponse>(paginatedData, request.PageNumber, request.PageSize, totalItems);
    }
}