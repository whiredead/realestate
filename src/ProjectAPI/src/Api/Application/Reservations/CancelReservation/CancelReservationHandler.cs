using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Reservations.Interface;

namespace ProjectAPI.Api.Application.Reservations.CancelReservation;

public class CancelReservationHandler : IRequestHandler<CancelReservationCommand, CancelReservationResponse>
{
    private readonly IReservationRepository _reservationRepository;

    public CancelReservationHandler(IReservationRepository reservationRepository)
    {
        _reservationRepository = reservationRepository;
    }

    public async Task<CancelReservationResponse> Handle(CancelReservationCommand request, CancellationToken cancellationToken)
    {
        // Validate that the reservation exists
        var reservation = await _reservationRepository.GetByIDAsync(request.ReservationId)
            ?? throw new NotFoundException($"Reservation with ID {request.ReservationId} not found.");

        // §12.4 — only an APPROVED reservation can be cancelled. Previously this
        // handler applied the change unconditionally, so an already Sold, Rejected
        // or Expired reservation could be "cancelled".
        ReservationStateMachine.EnsureCanTransition(reservation.Status, ReservationStatus.Cancelled);

        reservation.Status = ReservationStatus.Cancelled;

        // Save changes
        await _reservationRepository.SaveAsync();

        return new CancelReservationResponse
        {
            Success = true,
            Message = "Reservation cancelled successfully."
        };
    }
}