using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Handovers.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Handovers.GetMyWarranties;

public class GetMyWarrantiesHandler : IRequestHandler<GetMyWarrantiesQuery, List<MyWarrantyDto>>
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ProjectScopeService _projectScope;

    public GetMyWarrantiesHandler(ApplicationDbContext db, ICurrentUser currentUser, ProjectScopeService projectScope)
    {
        _db = db;
        _currentUser = currentUser;
        _projectScope = projectScope;
    }

    public async Task<List<MyWarrantyDto>> Handle(GetMyWarrantiesQuery request, CancellationToken ct)
    {
        var reservation = await _db.Set<Reservation>().FindAsync(new object?[] { request.ReservationId }, ct)
            ?? throw new NotFoundException($"Reservation {request.ReservationId} not found.");

        // N18 — same missing internal-caller path as GetMyHandoverStatusHandler.
        var isInternal = _currentUser.Roles.Any(r => RoleCodes.Internal.Contains(r, StringComparer.Ordinal));
        if (isInternal)
        {
            await _projectScope.EnsureReservationAccessAsync(request.ReservationId, ct);
        }
        else if (string.IsNullOrEmpty(reservation.BuyerId) || reservation.BuyerId != _currentUser.UserId)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.Unauthorized,
                "Cette garantie n'est pas accessible.",
                StatusCodes.Status403Forbidden);
        }

        return await _db.Set<Warranty>()
            .Where(w => w.ReservationId == request.ReservationId)
            .OrderByDescending(w => w.StartsAt)
            .Select(w => new MyWarrantyDto
            {
                Id = w.Id,
                UnitId = w.UnitId,
                WarrantyTypeCode = w.WarrantyTypeCode,
                StartsAt = w.StartsAt,
                EndsAt = w.EndsAt,
                IsActive = w.IsActive
            })
            .ToListAsync(ct);
    }
}
