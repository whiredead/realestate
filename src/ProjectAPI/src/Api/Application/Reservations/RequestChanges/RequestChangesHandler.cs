using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Reservations.Interface;

namespace ProjectAPI.Api.Application.Reservations.RequestChanges;

/// <summary>
/// Moves a submitted reservation to CHANGES_REQUESTED (spec §12.2).
///
/// The unit stays blocked so the buyer does not lose it while the agent
/// corrects the file. Only the reason is recorded here; the identity fields of
/// the unit, the original catalogue price and the submitting agent are not
/// modifiable during a correction (FR-RES-007).
/// </summary>
public class RequestChangesHandler : IRequestHandler<RequestChangesCommand, bool>
{
    private readonly IReservationRepository _reservationRepo;
    private readonly ProjectScopeService _projectScope;

    public RequestChangesHandler(IReservationRepository reservationRepo, ProjectScopeService projectScope)
    {
        _reservationRepo = reservationRepo;
        _projectScope = projectScope;
    }

    public async Task<bool> Handle(RequestChangesCommand request, CancellationToken ct)
    {
        // §6.4 — project-scoped: correction requests are an admin decision
        // within their assigned projects.
        await _projectScope.EnsureReservationAccessAsync(request.ReservationId, ct);

        var reservation = await _reservationRepo.GetByIDAsync(request.ReservationId)
            ?? throw new NotFoundException($"Reservation {request.ReservationId} not found.");

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "Un motif est obligatoire pour demander une correction.");
        }

        ReservationStateMachine.EnsureCanTransition(reservation.Status, ReservationStatus.ChangesRequested);

        reservation.Status = ReservationStatus.ChangesRequested;
        reservation.AdminNote = request.Reason;

        await _reservationRepo.Update(reservation);
        await _reservationRepo.SaveAsync();

        return true;
    }
}
