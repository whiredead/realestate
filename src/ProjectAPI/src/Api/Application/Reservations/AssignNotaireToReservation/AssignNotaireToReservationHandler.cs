using Microsoft.AspNetCore.Identity;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Reservations.Interface;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Api.Application.Reservations.AssignNotaireToReservation;

public class AssignNotaireToReservationHandler : IRequestHandler<AssignNotaireToReservationCommand, AssignNotaireToReservationResponse>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly UserManager<User> _userManager;
    private readonly ProjectScopeService _projectScope;

    public AssignNotaireToReservationHandler(
        IReservationRepository reservationRepository,
        UserManager<User> userManager,
        ProjectScopeService projectScope)
    {
        _reservationRepository = reservationRepository;
        _userManager = userManager;
        _projectScope = projectScope;
    }

    public async Task<AssignNotaireToReservationResponse> Handle(AssignNotaireToReservationCommand request, CancellationToken cancellationToken)
    {
        // Validate that the reservation exists
        var reservation = await _reservationRepository.GetByIDAsync(request.ReservationId)
            ?? throw new NotFoundException($"Reservation with ID {request.ReservationId} not found.");

        // §6.4 — the only reservation mutation that was missing this check;
        // every other reservation handler (Approve/Reject/Cancel/...) already
        // enforces it.
        await _projectScope.EnsureReservationAccessAsync(request.ReservationId, cancellationToken);

        // Validate that the notary exists if NotaireId is provided
        User? notary = null;
        if (!string.IsNullOrEmpty(request.NotaireId))
        {
            notary = await _userManager.FindByIdAsync(request.NotaireId)
                ?? throw new NotFoundException($"Notary with ID {request.NotaireId} not found.");
        }

        // Update the reservation's NotaireId
        reservation.NotaireId = notary?.Id;

        // Save changes
        await _reservationRepository.SaveAsync();

        return new AssignNotaireToReservationResponse
        {
            Success = true,
            Message = notary != null 
                ? $"Notary {notary.FirstName} {notary.LastName} assigned to reservation successfully."
                : "Notary removed from reservation successfully."
        };
    }
}