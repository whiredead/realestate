using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Reservations.Interface;

namespace ProjectAPI.Api.Application.Reservations.SoldReservation;

public class SoldReservationHandler : IRequestHandler<SoldReservationCommand, SoldReservationResponse>
{
    private readonly IReservationRepository _reservationRepository;

    public SoldReservationHandler(IReservationRepository reservationRepository)
    {
        _reservationRepository = reservationRepository;
    }

    public async Task<SoldReservationResponse> Handle(SoldReservationCommand request, CancellationToken cancellationToken)
    {
        // Validate that the reservation exists
        var reservation = await _reservationRepository.GetByIDAsync(request.ReservationId)
            ?? throw new NotFoundException($"Reservation with ID {request.ReservationId} not found.");

        // §12.4 — CONVERTED is only reachable from APPROVED. Previously unguarded,
        // so a Pending or already-Cancelled reservation could be marked as sold.
        ReservationStateMachine.EnsureCanTransition(reservation.Status, ReservationStatus.Sold);

        reservation.Status = ReservationStatus.Sold;

        // Save changes
        await _reservationRepository.SaveAsync();

        return new SoldReservationResponse
        {
            Success = true,
            Message = "Reservation marked as sold successfully."
        };
    }
}