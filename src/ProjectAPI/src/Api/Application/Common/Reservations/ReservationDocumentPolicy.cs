using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Reservations.Entities;

namespace ProjectAPI.Api.Application.Common.Reservations;

/// <summary>
/// When a reservation's documents may change.
///
/// Adding stays open on every live file, including after approval (the spec
/// keeps "documents" editable on an approved reservation). Removing a piece is
/// only allowed while the file is still being prepared or corrected (DRAFT,
/// CHANGES_REQUESTED): once submitted or decided, the documents are what the
/// decision was taken on. Nothing changes on a dead file.
/// </summary>
public static class ReservationDocumentPolicy
{
    public static bool CanAdd(ReservationStatus status) =>
        status is not (ReservationStatus.Rejected or ReservationStatus.Cancelled or ReservationStatus.Expired);

    public static bool CanDelete(ReservationStatus status) =>
        status is ReservationStatus.Draft or ReservationStatus.ChangesRequested;

    public static void EnsureCanAdd(ReservationStatus status)
    {
        if (!CanAdd(status))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.InvalidStatusTransition,
                $"Aucun document ne peut être ajouté à une réservation au statut {status}.",
                StatusCodes.Status409Conflict);
        }
    }

    public static void EnsureCanDelete(ReservationStatus status)
    {
        if (!CanDelete(status))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.InvalidStatusTransition,
                $"Un document ne peut être supprimé que sur une réservation en brouillon ou en correction (statut actuel : {status}).",
                StatusCodes.Status409Conflict);
        }
    }
}
