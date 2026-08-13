using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Handovers.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Handovers.GetMyHandoverStatus;

public class GetMyHandoverStatusHandler : IRequestHandler<GetMyHandoverStatusQuery, MyHandoverStatusDto?>
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ProjectScopeService _projectScope;

    public GetMyHandoverStatusHandler(ApplicationDbContext db, ICurrentUser currentUser, ProjectScopeService projectScope)
    {
        _db = db;
        _currentUser = currentUser;
        _projectScope = projectScope;
    }

    public async Task<MyHandoverStatusDto?> Handle(GetMyHandoverStatusQuery request, CancellationToken ct)
    {
        var reservation = await _db.Set<Reservation>().FindAsync(new object?[] { request.ReservationId }, ct)
            ?? throw new NotFoundException($"Reservation {request.ReservationId} not found.");

        // N18 — despite HandoverPanel.tsx being built for both the buyer AND
        // agent/admin (it takes a canManage prop and renders the schedule/
        // confirm/report actions for staff), this route name ("mine") and its
        // handler had literally no path for an internal caller: it only ever
        // checked reservation.BuyerId == caller, so any agent or admin viewing
        // a reservation they are legitimately scoped to was refused outright.
        // Mirrors GetReservationByIdHandler/GetMyFinalVisitReportHandler's own
        // buyer-owns-OR-internal-is-project-scoped shape.
        var isInternal = _currentUser.Roles.Any(r => RoleCodes.Internal.Contains(r, StringComparer.Ordinal));
        if (isInternal)
        {
            await _projectScope.EnsureReservationAccessAsync(request.ReservationId, ct);
        }
        else if (string.IsNullOrEmpty(reservation.BuyerId) || reservation.BuyerId != _currentUser.UserId)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.Unauthorized,
                "Ce dossier de livraison n'est pas accessible.",
                StatusCodes.Status403Forbidden);
        }

        var appointment = await _db.Set<HandoverAppointment>()
            .Include(a => a.Reports).ThenInclude(r => r.Items)
            .Where(a => a.ReservationId == request.ReservationId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (appointment == null) return null;

        var latestReport = appointment.Reports.OrderByDescending(r => r.VersionNo).FirstOrDefault();

        return new MyHandoverStatusDto
        {
            AppointmentId = appointment.Id,
            ScheduledAt = appointment.ScheduledAt,
            EndsAt = appointment.EndsAt,
            Location = appointment.Location,
            AppointmentStatus = (int)appointment.Status,
            ReportId = latestReport?.Id,
            ReportVersionNo = latestReport?.VersionNo,
            ReportStatus = latestReport != null ? (int)latestReport.Status : null,
            ReportSubmittedAt = latestReport?.SubmittedAt,
            AcknowledgedAt = latestReport?.AcknowledgedAt,
            Items = latestReport?.Items.Select(i => new MyHandoverItemDto
            {
                ItemType = i.ItemType,
                Quantity = i.Quantity,
                Comment = i.Comment
            }).ToList() ?? new List<MyHandoverItemDto>()
        };
    }
}
