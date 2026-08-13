using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Notary;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Common.Units;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;
using UnitEntity = ProjectAPI.Domain.Immeubles.Entities.Unit;

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
/// Confirming re-derives eligibility via <see cref="NotaryEligibilityService"/>
/// (§18.3 FR-NOT-002): a file eligible when requested may no longer be by the
/// time it is confirmed (a new snag, a disputed report), so the check cannot
/// live only at creation.
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
    private readonly INotaryAppointmentAssignmentHistoryRepository _historyRepository;
    private readonly IUnitStatusService _unitStatus;
    private readonly ICurrentUser _currentUser;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<UpdateNotaryAppointmentHandler> _logger;
    private readonly ProjectScopeService _projectScope;
    private readonly NotaryEligibilityService _eligibility;

    public UpdateNotaryAppointmentHandler(
        INotaryAppointmentRepository notaryAppointmentRepository,
        INotaryAppointmentAssignmentHistoryRepository historyRepository,
        IUnitStatusService unitStatus,
        ICurrentUser currentUser,
        ApplicationDbContext db,
        ILogger<UpdateNotaryAppointmentHandler> logger,
        ProjectScopeService projectScope,
        NotaryEligibilityService eligibility)
    {
        _notaryAppointmentRepository = notaryAppointmentRepository;
        _historyRepository = historyRepository;
        _unitStatus = unitStatus;
        _currentUser = currentUser;
        _db = db;
        _logger = logger;
        _projectScope = projectScope;
        _eligibility = eligibility;
    }

    public async Task<UpdateNotaryAppointmentResponse> Handle(UpdateNotaryAppointmentCommand request, CancellationToken cancellationToken)
    {
        var notaryAppointment = await _notaryAppointmentRepository.GetByIDAsync(request.Id)
            ?? throw new NotFoundException($"Notary appointment with ID {request.Id} not found.");

        // §6.4 — confirming/recording an outcome is the assigned notary's or a
        // scoped admin's act; a notary from another project must not reach this.
        await _projectScope.EnsureReservationAccessAsync(notaryAppointment.ReservationId, cancellationToken);

        // Project scope only proved this notary works on the project — not
        // that THIS appointment is theirs. Without this, any NOTARY on the
        // project could act on a colleague's appointment (mirrors the
        // SALES_AGENT ownership check on commercial appointments). Admins
        // and the requesting agent/buyer path are unaffected — this only
        // narrows a NOTARY caller.
        if (_currentUser.IsInRole(RoleCodes.Notary) && notaryAppointment.NotaireId != _currentUser.UserId)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.Unauthorized,
                "Ce rendez-vous notarial n'est pas affecté à votre compte.",
                StatusCodes.Status403Forbidden);
        }

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

            // §18.3 FR-NOT-002 — eligibility is "recomputed at creation AND
            // confirmation": snags can be raised or the report disputed between
            // the request and the confirmation, so re-deriving it only at
            // creation would let a now-ineligible file sail through.
            if (targetStatus == AppointmentAttemptStatus.Confirmed && currentStatus != targetStatus)
            {
                var reservationForEligibility = await _db.Reservations
                    .FirstOrDefaultAsync(r => r.Id == notaryAppointment.ReservationId, cancellationToken)
                    ?? throw new NotFoundException($"Reservation {notaryAppointment.ReservationId} not found.");

                await _eligibility.EnsureEligibleAsync(
                    notaryAppointment.ReservationId, reservationForEligibility.UnitId, cancellationToken);

                // Eligibility was re-derived at confirmation, but slot
                // availability was not — a slot open at request time could
                // have become double-booked (another appointment or a new
                // NotaryBlock) by confirmation. Mirrors CreateNotaryAppointmentHandler's
                // own check, excluding this row itself.
                if (!string.IsNullOrEmpty(notaryAppointment.NotaireId))
                {
                    await EnsureSlotStillAvailableAsync(notaryAppointment.Id, notaryAppointment.NotaireId, notaryAppointment.AppointmentDate, cancellationToken);
                }
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

        if (!string.IsNullOrWhiteSpace(request.NewNotaireId))
        {
            if (string.IsNullOrWhiteSpace(request.ReassignmentReason))
            {
                throw new BusinessRuleException(
                    BusinessErrorCodes.ValidationFailed,
                    "Un motif est obligatoire pour réaffecter un rendez-vous notarial.");
            }

            var reservationForReassignment = await _db.Reservations
                .FirstOrDefaultAsync(r => r.Id == notaryAppointment.ReservationId, cancellationToken)
                ?? throw new NotFoundException($"Reservation {notaryAppointment.ReservationId} not found.");

            await EnsureEligibleNotaryAsync(reservationForReassignment.UnitId, request.NewNotaireId, cancellationToken);
            await EnsureSlotStillAvailableAsync(notaryAppointment.Id, request.NewNotaireId, notaryAppointment.AppointmentDate, cancellationToken);

            var previousNotaireId = notaryAppointment.NotaireId;
            var wasConfirmed = string.Equals(notaryAppointment.Status, AppointmentAttemptStatus.Confirmed.ToString(), StringComparison.OrdinalIgnoreCase);

            if (wasConfirmed)
            {
                // Never silently change a confirmed appointment: freeze this
                // row (Superseded) and create a new one carrying the
                // reassignment, pending the buyer's acceptance — mirrors
                // UpdateAppointmentStatusHandler's commercial-appointment logic.
                notaryAppointment.Status = AppointmentAttemptStatus.Superseded.ToString();

                var replacement = new NotaryAppointment
                {
                    Id = Guid.NewGuid(),
                    BuyerId = notaryAppointment.BuyerId,
                    NotaireId = request.NewNotaireId,
                    AgentId = notaryAppointment.AgentId,
                    ConnectedUserId = notaryAppointment.ConnectedUserId,
                    ReservationId = notaryAppointment.ReservationId,
                    AppointmentDate = notaryAppointment.AppointmentDate,
                    Status = AppointmentAttemptStatus.Requested.ToString(),
                    BuyerFirstName = notaryAppointment.BuyerFirstName,
                    BuyerLastName = notaryAppointment.BuyerLastName,
                    BuyerCIN = notaryAppointment.BuyerCIN,
                    BuyerEmail = notaryAppointment.BuyerEmail,
                    BuyerPhoneNumber = notaryAppointment.BuyerPhoneNumber,
                    PropertyPrice = notaryAppointment.PropertyPrice,
                    TaxFees = notaryAppointment.TaxFees,
                    TahfidFees = notaryAppointment.TahfidFees,
                    PreviousAppointmentId = notaryAppointment.Id,
                    PreviousNotaireId = previousNotaireId,
                    ReassignmentReason = request.ReassignmentReason,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Add(replacement);
                await _db.Set<NotaryAppointmentAssignmentHistory>().AddAsync(new NotaryAppointmentAssignmentHistory
                {
                    Id = Guid.NewGuid(),
                    NotaryAppointmentId = replacement.Id,
                    NotaireId = request.NewNotaireId,
                    PreviousNotaireId = previousNotaireId,
                    AssignmentSource = "MANUAL_REASSIGNMENT",
                    Reason = request.ReassignmentReason,
                    ActorUserId = request.ActorUserId ?? _currentUser.UserId,
                    AssignedAt = replacement.CreatedAt
                }, cancellationToken);

                await _notaryAppointmentRepository.SaveAsync();

                return new UpdateNotaryAppointmentResponse
                {
                    Success = true,
                    AppointmentId = replacement.Id,
                    Status = replacement.Status,
                    Message = "Rendez-vous notarial réaffecté ; en attente d'acceptation du nouveau créneau par l'acheteur."
                };
            }

            // Not yet confirmed: nothing was promised to the buyer, so an
            // in-place change is safe.
            notaryAppointment.PreviousNotaireId = previousNotaireId;
            notaryAppointment.NotaireId = request.NewNotaireId;
            notaryAppointment.ReassignmentReason = request.ReassignmentReason;

            await _db.Set<NotaryAppointmentAssignmentHistory>().AddAsync(new NotaryAppointmentAssignmentHistory
            {
                Id = Guid.NewGuid(),
                NotaryAppointmentId = notaryAppointment.Id,
                NotaireId = request.NewNotaireId,
                PreviousNotaireId = previousNotaireId,
                AssignmentSource = "MANUAL_REASSIGNMENT",
                Reason = request.ReassignmentReason,
                ActorUserId = request.ActorUserId ?? _currentUser.UserId,
                AssignedAt = DateTime.UtcNow
            }, cancellationToken);

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

                // §5.7/§5.8 — "reservation → CONVERTED and unit → SOLD
                // atomically" never included creating the Sale record itself:
                // /admin/sales, sales KPIs and the unit journey's "Vente"
                // stage all read this table, and nothing in the codebase ever
                // wrote to it (N15). ScheduleDeliveryHandler also requires an
                // existing Sale — though the newer HandoversController path
                // (ScheduleHandoverHandler) doesn't, so handovers were never
                // actually blocked by this gap, only sales reporting was.
                if (!await _db.Set<Sale>().AnyAsync(s => s.UnitId == reservation.UnitId, cancellationToken))
                {
                    _db.Add(new Sale
                    {
                        Id = Guid.NewGuid(),
                        BuyerId = reservation.BuyerId,
                        BuyerFirstName = reservation.Name ?? string.Empty,
                        BuyerLastName = reservation.LastName ?? string.Empty,
                        BuyerEmail = reservation.Email ?? string.Empty,
                        BuyerPhoneNumber = reservation.PhoneNumber ?? string.Empty,
                        BuyerCIN = reservation.CIN,
                        UnitId = reservation.UnitId,
                        SaleDate = DateTime.UtcNow,
                        TotalPrice = reservation.FinalPrice ?? reservation.TotalPropertyPrice,
                        IsUnderConstruction = reservation.IsUnderConstruction
                    });
                }

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
            AppointmentId = notaryAppointment.Id,
            Status = notaryAppointment.Status,
            Message = converted
                ? "Rendez-vous notarial mis à jour : achat finalisé, réservation convertie et bien vendu."
                : "Notary appointment updated successfully."
        };
    }

    /// <summary>The chosen notary must hold an active NOTARY ProjectMembership on the project the reservation's unit belongs to. Mirrors CreateNotaryAppointmentHandler's own check.</summary>
    private async Task EnsureEligibleNotaryAsync(Guid unitId, string notaryId, CancellationToken ct)
    {
        var projectId = await _db.Set<UnitEntity>()
            .Where(u => u.Id == unitId)
            .Join(_db.Set<Immeuble>(), u => u.ProjectId, im => im.Id, (u, im) => im.ProjectId)
            .FirstOrDefaultAsync(ct);

        if (projectId == Guid.Empty)
        {
            throw new NotFoundException($"Unit {unitId} not found.");
        }

        var now = DateTime.UtcNow;
        var isEligible = await _db.Set<ProjectMembership>().AnyAsync(m =>
            m.ProjectId == projectId &&
            m.UserId == notaryId &&
            m.RoleCode == RoleCodes.Notary &&
            m.IsActive &&
            m.ValidFrom <= now &&
            (m.ValidUntil == null || m.ValidUntil > now), ct);

        if (!isEligible)
        {
            throw new Common.Exceptions.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure(nameof(UpdateNotaryAppointmentCommand.NewNotaireId),
                    $"Le notaire {notaryId} n'a pas d'affectation NOTARY active sur ce projet.")
            });
        }
    }

    /// <summary>Rejects a slot that overlaps another active appointment or a block for the given notary, excluding the appointment being confirmed/reassigned itself.</summary>
    private async Task EnsureSlotStillAvailableAsync(Guid excludeAppointmentId, string notaryId, DateTime appointmentDate, CancellationToken ct)
    {
        var blockingStatuses = AppointmentStateMachine.BlockingStatuses.Select(s => s.ToString()).ToArray();
        var conflictWindowStart = appointmentDate.AddMinutes(-1);
        var conflictWindowEnd = appointmentDate.AddMinutes(1);

        var hasConflict = await _db.Set<NotaryAppointment>().AnyAsync(a =>
            a.Id != excludeAppointmentId &&
            a.NotaireId == notaryId &&
            blockingStatuses.Contains(a.Status) &&
            a.AppointmentDate > conflictWindowStart &&
            a.AppointmentDate < conflictWindowEnd, ct);

        if (hasConflict)
        {
            throw BusinessRuleException.AppointmentSlotConflict(appointmentDate);
        }

        var blocked = await _db.Set<Domain.Users.Entities.NotaryBlock>().AnyAsync(b =>
            b.NotaryId == notaryId &&
            b.Start <= appointmentDate &&
            b.End > appointmentDate, ct);

        if (blocked)
        {
            throw BusinessRuleException.AppointmentSlotConflict(appointmentDate);
        }
    }
}
