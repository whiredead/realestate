using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Immeubles.Interfaces;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Api.Application.Immeubles.GetAllImmeubles;

/// <summary>
/// Handler for getting all projects.
/// </summary>
public class GetAllImmeublesHandler : IRequestHandler<GetAllImmeublesQuery, PaginatedResponse<ImmeubleResponse>>
{
    private readonly IImmeubleRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly ProjectScopeService _projectScope;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetAllImmeublesHandler"/> class.
    /// </summary>
    /// <param name="repository">The project repository to access project data.</param>
    /// <param name="currentUser">Identifies whether the caller is internal staff vs. a public/anonymous visitor.</param>
    /// <param name="projectScope">Enforces §6.4: an internal caller below GLOBAL_ADMIN sees only their assigned projects' buildings.</param>
    public GetAllImmeublesHandler(IImmeubleRepository repository, ICurrentUser currentUser, ProjectScopeService projectScope)
    {
        _repository = repository;
        _currentUser = currentUser;
        _projectScope = projectScope;
    }

    /// <summary>
    /// Handles the query to get all projects.
    /// </summary>
    /// <param name="request">The query containing pagination and filtering details.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>A paginated response containing the projects.</returns>
    public async Task<PaginatedResponse<ImmeubleResponse>> Handle(GetAllImmeublesQuery request, CancellationToken cancellationToken)
    {
        // §3/§7 — NumberOfAvailableUnites/NumberOfSoldUnites/NumberOfUnits on
        // Immeuble are legacy denormalized counters nothing in the codebase
        // ever writes to (confirmed: no handler assigns them after creation),
        // so they are permanently stale. This line even copy-pasted
        // NumberOfUnits into NumberOfAvailableUnites, showing every unit as
        // "available" regardless of sales. Unit.Status is the one
        // continuously-maintained source of truth (UnitStatusService), so
        // stock figures are computed live from it instead.
        var projects = await _repository.Find(
            p => (string.IsNullOrEmpty(request.Name) || p.Name.Contains(request.Name)) &&
                 (string.IsNullOrEmpty(request.Location) || p.Location!.Contains(request.Location)) &&
                 (!request.ProjectId.HasValue || p.ProjectId == request.ProjectId) &&
                 (request.MinPrice == null || p.MinPrice >= request.MinPrice) &&
                 (request.MaxPrice == null || p.MaxPrice <= request.MaxPrice) &&
                 (request.Type == null || p.Type == request.Type.ToString()) &&
                 (request.MinSellableSurfaceRange == null || p.MinSellableSurfaceRange >= request.MinSellableSurfaceRange) &&
                 (request.MaxSellableSurfaceRange == null || p.MaxSellableSurfaceRange <= request.MaxSellableSurfaceRange),
            p => p.PlanInterieurs,
            p => p.Units
        );

        // §6.3/§6.4 — same reasoning as GetAllProjectsHandler: this route is
        // [AllowAnonymous] for the public catalogue, but the admin console's
        // building list (ImmeublesListPage.tsx) hits the same endpoint. An
        // internal caller below GLOBAL_ADMIN must only see buildings inside
        // their own ProjectMembership perimeter (F7 regression).
        var isInternal = _currentUser.IsAuthenticated && RoleCodes.Internal.Any(_currentUser.IsInRole);
        if (isInternal)
        {
            var scope = await _projectScope.GetScopedProjectIdsAsync(cancellationToken);
            if (scope is not null)
            {
                projects = projects.Where(p => scope.Contains(p.ProjectId)).ToList();
            }
        }

        var totalItems = projects.Count();
        var paginatedData = projects
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p =>
            {
                var totalUnits = p.Units.Count;
                var availableUnits = p.Units.Count(u => u.Status == UnitCommercialStatus.Available);
                var soldUnits = p.Units.Count(u =>
                    u.Status == UnitCommercialStatus.Sold || u.Status == UnitCommercialStatus.Delivered);
                // The in-between states. Reporting only sold + available left
                // every reserved unit invisible, so the published figures never
                // summed to NumberOfUnits (§3/§7 — same status grouping the
                // dashboard's inventory snapshot already uses).
                var reservedUnits = p.Units.Count(u =>
                    u.Status == UnitCommercialStatus.HoldPendingApproval ||
                    u.Status == UnitCommercialStatus.Reserved ||
                    u.Status == UnitCommercialStatus.Contracted);

                return new ImmeubleResponse
                {
                    Id = p.Id,
                    ProjectId = p.ProjectId,
                    AgentId = p.AgentId,
                    Name = p.Name,
                    Location = p.Location,
                    Type = p.Type,
                    MinPrice = p.MinPrice,
                    MaxPrice = p.MaxPrice,
                    // Normalize, not a fixed enum: Immeuble.Status is free
                    // text and now carries canonical §3 codes as well as
                    // legacy spellings (same shape as Project.StatusGlobal).
                    Status = ProjectStatusCodes.Normalize(p.Status),
                    // Matches GetImmeubleByIdHandler's null-safety for the
                    // same field.
                    Images = p.Images != null ? [.. p.Images.Split(',')] : [],
                    Description = p.Description,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    NumberOfUnits = totalUnits,
                    MaxSellableSurfaceRange = p.MaxSellableSurfaceRange,
                    MinSellableSurfaceRange = p.MinSellableSurfaceRange,
                    Module3DLink = p.Module3DLink,
                    NumberOfAvailableUnites = availableUnits,
                    NumberOfSoldUnites = soldUnits,
                    NumberOfReservedUnites = reservedUnits,
                    SellsPercentage = totalUnits > 0 ? (int)Math.Round(soldUnits * 100.0 / totalUnits) : 0,
                    PlanInerieurs = p.PlanInterieurs.Select(pi => new PlanInerieurResponse
                    {
                        Id = pi.Id,
                        ImmeubleId = pi.ImmeubleId,
                        PhotoLinks = pi.PhotoLinks
                    }).ToList()
                };
            });

        return new PaginatedResponse<ImmeubleResponse>(paginatedData, request.PageNumber, request.PageSize, totalItems);
    }
}