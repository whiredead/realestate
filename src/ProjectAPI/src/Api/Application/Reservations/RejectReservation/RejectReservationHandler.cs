using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Reservations.Interface;
using ValidationException = FluentValidation.ValidationException;

namespace ProjectAPI.Api.Application.Reservations.RejectReservation;

public class RejectReservationHandler : IRequestHandler<RejectReservationCommand, bool>
{
    private readonly IReservationRepository _reservationRepo;

    public RejectReservationHandler(IReservationRepository reservationRepo)
    {
        _reservationRepo = reservationRepo;
    }

    public async Task<bool> Handle(RejectReservationCommand request, CancellationToken ct)
    {
        var reservation = await _reservationRepo.GetByIDAsync(request.ReservationId)
            ?? throw new NotFoundException($"Reservation {request.ReservationId} not found.");

        // §12.4 — REJECTED is reachable from SUBMITTED and CHANGES_REQUESTED.
        ReservationStateMachine.EnsureCanTransition(reservation.Status, ReservationStatus.Rejected);

        reservation.Status = ReservationStatus.Rejected;
        reservation.ValidatedAt = DateTime.UtcNow;
        reservation.ValidatedBy = request.AdminUserId;
        reservation.AdminNote = request.Reason;

        _reservationRepo.Update(reservation);
        await _reservationRepo.SaveAsync();
        return true;
    }
}
