using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Reservations.Interface;

namespace ProjectAPI.Api.Application.Reservations.ResubmitReservation;

/// <summary>
/// Submits a DRAFT, or returns a corrected CHANGES_REQUESTED reservation to
/// SUBMITTED (spec §12.4).
///
/// A DRAFT does not block its unit (§12.1), so submitting one must re-check
/// availability: the unit may have been taken in the meantime. The filtered
/// index IX_Reservations_ActivePerUnit remains the concurrency backstop.
/// </summary>
public class ResubmitReservationHandler : IRequestHandler<ResubmitReservationCommand, bool>
{
    private readonly IReservationRepository _reservationRepo;

    public ResubmitReservationHandler(IReservationRepository reservationRepo)
    {
        _reservationRepo = reservationRepo;
    }

    public async Task<bool> Handle(ResubmitReservationCommand request, CancellationToken ct)
    {
        var reservation = await _reservationRepo.GetByIDAsync(request.ReservationId)
            ?? throw new NotFoundException($"Reservation {request.ReservationId} not found.");

        ReservationStateMachine.EnsureCanTransition(reservation.Status, ReservationStatus.Pending);

        // Submitting a draft is the moment the unit gets blocked, so re-check
        // that nobody else claimed it while the draft sat unsubmitted.
        if (reservation.Status == ReservationStatus.Draft)
        {
            var competing = await _reservationRepo.Find(r =>
                r.UnitId == reservation.UnitId &&
                r.Id != reservation.Id &&
                (r.Status == ReservationStatus.Pending ||
                 r.Status == ReservationStatus.ChangesRequested ||
                 r.Status == ReservationStatus.Approved ||
                 r.Status == ReservationStatus.Sold));

            if (competing.Any())
            {
                throw BusinessRuleException.UnitNotAvailable(reservation.UnitId);
            }
        }

        reservation.Status = ReservationStatus.Pending;

        if (!string.IsNullOrWhiteSpace(request.AgentNote))
        {
            reservation.AdminNote = request.AgentNote;
        }

        await _reservationRepo.Update(reservation);
        await _reservationRepo.SaveAsync();

        return true;
    }
}
