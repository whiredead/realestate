using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Common.Units;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.NotaryAppointments.UpdateNotaryAppointment;

/// <summary>
/// Updates a notary appointment (spec §18.4 FR-NOT-004) and records its outcome
/// (§5.7).
///
/// Status changes are gated by <see cref="AppointmentStateMachine"/> — the same
/// matrix used for final-visit appointments (§5.1) — instead of accepting any
/// string. A notary appointment can no longer jump directly from, say,
/// `Requested` to `Completed` without going through `Confirmed` first.
///
/// The outcome is the business event, and it is deliberately separate from the
/// status: completing an appointment REQUIRES one, and only PURCHASE_COMPLETED
/// converts the reservation and sells the unit — atomically, per §5.7. Every
/// other outcome closes the meeting and leaves the file untouched so it can be
/// retried with a new appointment.
/// </summary>
public class UpdateNotaryAppointmentHandler : IRequestHandler<UpdateNotaryAppointmentCommand, UpdateNotaryAppointmentResponse>
{
    private readonly INotaryAppointmentRepository _notaryAppointmentRepository;
    private readonly IUnitStatusService _unitStatus;
    private readonly ICurrentUser _currentUser;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<UpdateNotaryAppointmentHandler> _logger;

    public UpdateNotaryAppointmentHandler(
        INotaryAppointmentRepository notaryAppointmentRepository,
        IUnitStatusService unitStatus,
        ICurrentUser currentUser,
        ApplicationDbContext db,
        ILogger<UpdateNotaryAppointmentHandler> logger)
    {
        _notaryAppointmentRepository = notaryAppointmentRepository;
        _unitStatus = unitStatus;
        _currentUser = currentUser;
        _db = db;
        _logger = logger;
    }

    public async Task<UpdateNotaryAppointmentResponse> Handle(UpdateNotaryAppointmentCommand request, CancellationToken cancellationToken)
    {
        var notaryAppointment = await _notaryAppointmentRepository.GetByIDAsync(request.Id)
            ?? throw new NotFoundException($"Notary appointment with ID {request.Id} not found.");

        var hasUpdates = false;
        var completing = false;

        if (request.Status != null)
        {
            // The validator already rejects unknown names; a parse failure here
            // would mean the persisted value predates this enum (legacy data).
            if (!Enum.TryParse<AppointmentAttemptStatus>(notaryAppointment.Status, out var currentStatus))
            {
                currentStatus = AppointmentAttemptStatus.Requested;
            }

            var targetStatus = Enum.Parse<AppointmentAttemptStatus>(request.Status);

            if (currentStatus != targetStatus && !AppointmentStateMachine.CanTransition(currentStatus, targetStatus))
            {
                throw BusinessRuleException.InvalidStatusTransition(currentStatus.ToString(), targetStatus.ToString());
            }

            completing = targetStatus == AppointmentAttemptStatus.Completed && currentStatus != targetStatus;

            notaryAppointment.Status = targetStatus.ToString();
            hasUpdates = true;
        }

        // §5.7 — a completed appointment without a recorded outcome is exactly the
        // hole that let a sale "complete" with no notarial act behind it.
        if (completing && string.IsNullOrWhiteSpace(request.Outcome))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "Un rendez-vous notarial ne peut être marqué réalisé sans résultat (§5.7) : " +
                string.Join(", ", NotaryOutcomeCodes.All) + ".");
        }

        NotaryAppointmentOutcome? outcome = null;
        if (!string.IsNullOrWhiteSpace(request.Outcome))
        {
            outcome = NotaryOutcomeCodes.Parse(request.Outcome);

            // An outcome is a record of what happened at the meeting, so it only
            // makes sense once the meeting is over.
            var effectiveStatus = notaryAppointment.Status;
            if (!string.Equals(effectiveStatus, AppointmentAttemptStatus.Completed.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessRuleException(
                    BusinessErrorCodes.InvalidStatusTransition,
                    "Un résultat ne peut être enregistré que sur un rendez-vous réalisé.",
                    StatusCodes.Status409Conflict);
            }

            if (notaryAppointment.Outcome.HasValue && notaryAppointment.Outcome != outcome)
            {
                throw new BusinessRuleException(
                    BusinessErrorCodes.InvalidStatusTransition,
                    "Le résultat de ce rendez-vous a déjà été enregistré et ne peut être modifié.",
                    StatusCodes.Status409Conflict);
            }

            notaryAppointment.Outcome = outcome;
            notaryAppointment.OutcomeRecordedAt = DateTime.UtcNow;
            notaryAppointment.OutcomeRecordedBy = _currentUser.UserId;
            notaryAppointment.OutcomeNote = request.OutcomeNote;
            hasUpdates = true;
        }

        if (request.TaxFees.HasValue)
        {
            notaryAppointment.TaxFees = request.TaxFees.Value;
            hasUpdates = true;
        }

        if (request.TahfidFees.HasValue)
        {
            notaryAppointment.TahfidFees = request.TahfidFees.Value;
            hasUpdates = true;
        }

        if (!hasUpdates)
        {
            return new UpdateNotaryAppointmentResponse
            {
                Success = false,
                Message = "No fields provided for update."
            };
        }

        // §5.7 — "Only PURCHASE_COMPLETED changes anything: reservation →
        // CONVERTED and unit → SOLD atomically."
        var converted = false;
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        if (outcome == NotaryAppointmentOutcome.PurchaseCompleted)
        {
            var reservation = await _db.Reservations
                .FirstOrDefaultAsync(r => r.Id == notaryAppointment.ReservationId, cancellationToken)
                ?? throw new NotFoundException($"Reservation {notaryAppointment.ReservationId} not found.");

            // Idempotent: a file already converted stays converted rather than
            // failing the whole update (§5.8's re-validation principle).
            if (reservation.Status != ReservationStatus.Sold)
            {
                ReservationStateMachine.EnsureCanTransition(reservation.Status, ReservationStatus.Sold);
                reservation.Status = ReservationStatus.Sold;

                await _unitStatus.TransitionAsync(
                    reservation.UnitId,
                    UnitCommercialStatus.Sold,
                    UnitStatusCause.NotaryPurchaseCompleted,
                    reservationId: reservation.Id,
                    actorUserId: _currentUser.UserId,
                    ct: cancellationToken);

                converted = true;
            }
        }

        await _notaryAppointmentRepository.SaveAsync();
        await transaction.CommitAsync(cancellationToken);

        if (converted)
        {
            _logger.LogInformation(
                "[NotaryOutcome] PURCHASE_COMPLETED on appointment {AppointmentId}: reservation {ReservationId} CONVERTED, unit SOLD",
                notaryAppointment.Id, notaryAppointment.ReservationId);
        }

        return new UpdateNotaryAppointmentResponse
        {
            Success = true,
            Message = converted
                ? "Rendez-vous notarial mis à jour : achat finalisé, réservation convertie et bien vendu."
                : "Notary appointment updated successfully."
        };
    }
}
