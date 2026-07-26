using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Units;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Reservations.Interface;
using ProjectAPI.Infrastructure.Context;
using ValidationException = FluentValidation.ValidationException;

namespace ProjectAPI.Api.Application.Reservations.RejectReservation;

public class RejectReservationHandler : IRequestHandler<RejectReservationCommand, bool>
{
    private readonly IReservationRepository _reservationRepo;
    private readonly IUnitStatusService _unitStatus;
    private readonly ApplicationDbContext _db;

    public RejectReservationHandler(
        IReservationRepository reservationRepo,
        IUnitStatusService unitStatus,
        ApplicationDbContext db)
    {
        _reservationRepo = reservationRepo;
        _unitStatus = unitStatus;
        _db = db;
    }

    public async Task<bool> Handle(RejectReservationCommand request, CancellationToken ct)
    {
        var reservation = await _reservationRepo.GetByIDAsync(request.ReservationId)
            ?? throw new NotFoundException($"Reservation {request.ReservationId} not found.");

        // §12.4 — REJECTED is reachable from SUBMITTED and CHANGES_REQUESTED.
        ReservationStateMachine.EnsureCanTransition(reservation.Status, ReservationStatus.Rejected);

        // §5.3 "Reject ⇒ reason mandatory, unit back to AVAILABLE". Rejection and
        // release commit together: a rejected reservation that left its unit held
        // would strand the unit permanently.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        reservation.Status = ReservationStatus.Rejected;
        reservation.ValidatedAt = DateTime.UtcNow;
        reservation.ValidatedBy = request.AdminUserId;
        reservation.AdminNote = request.Reason;

        await _unitStatus.TransitionAsync(
            reservation.UnitId,
            UnitCommercialStatus.Available,
            UnitStatusCause.ReservationRejected,
            reservationId: reservation.Id,
            actorUserId: request.AdminUserId,
            reason: request.Reason,
            ct: ct);

        await _reservationRepo.Update(reservation);
        await _reservationRepo.SaveAsync();

        await transaction.CommitAsync(ct);
        return true;
    }
}
