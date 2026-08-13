using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Notary;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Payments;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Purchases.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;
using UnitEntity = ProjectAPI.Domain.Immeubles.Entities.Unit;
namespace ProjectAPI.Api.Application.Notary.Appointments.CreateNotaryAppointment;

/// <summary>
/// Creates a notary appointment (spec §18.3 FR-NOT-002).
///
/// A request is only accepted when:
///   - the dossier is ELIGIBLE or ELIGIBLE_WITH_MINOR_SNAGS (§17.6, via the
///     shared NotaryEligibilityService — also re-checked at confirmation,
///     see UpdateNotaryAppointmentHandler);
///   - the unit's title status allows a notary appointment (§18.3);
///   - the requested slot does not overlap another active appointment or a
///     notary block for the same notary (§10.1, §30.4).
///
/// The old code created a <see cref="Purchase"/> here with a hardcoded
/// <c>PaidAmount</c>, bypassing the immutable payment ledger (§14.3). It now
/// creates the purchase shell with a zero balance and lets
/// <see cref="PurchaseTotalsService"/> derive the real total from the ledger,
/// same as every other purchase (§5.8).
/// </summary>
public class CreateNotaryAppointmentHandler : IRequestHandler<CreateNotaryAppointmentCommand, CreateNotaryAppointmentResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly PurchaseTotalsService _purchaseTotals;
    private readonly ProjectScopeService _projectScope;
    private readonly NotaryEligibilityService _eligibility;

    public CreateNotaryAppointmentHandler(
        ApplicationDbContext db,
        PurchaseTotalsService purchaseTotals,
        ProjectScopeService projectScope,
        NotaryEligibilityService eligibility)
    {
        _db = db;
        _purchaseTotals = purchaseTotals;
        _projectScope = projectScope;
        _eligibility = eligibility;
    }

    public async Task<CreateNotaryAppointmentResponse> Handle(CreateNotaryAppointmentCommand request, CancellationToken ct)
    {
        // §6.4 — requester (buyer, responsible agent or admin, per §5.7) must
        // own or be scoped to this reservation's project.
        await _projectScope.EnsureReservationAccessAsync(request.ReservationId, ct);
        await _projectScope.EnsureBuyerOwnsReservationAsync(request.ReservationId, ct);

        var reservation = await _db.Set<Reservation>()
            .FirstOrDefaultAsync(r => r.Id == request.ReservationId, ct)
            ?? throw new NotFoundException($"Reservation with ID {request.ReservationId} not found.");

        await _eligibility.EnsureEligibleAsync(request.ReservationId, reservation.UnitId, ct);

        if (!string.IsNullOrEmpty(request.NotaireId))
        {
            await EnsureEligibleNotaryAsync(reservation.UnitId, request.NotaireId, ct);
            await EnsureSlotAvailableAsync(request.NotaireId, request.AppointmentDate, ct);
        }

        var notaryAppointment = new NotaryAppointment
        {
            Id = Guid.NewGuid(),
            BuyerId = request.BuyerId,
            NotaireId = request.NotaireId,
            AgentId = request.AgentId,
            ConnectedUserId = request.ConnectedUserId,
            ReservationId = request.ReservationId,
            AppointmentDate = request.AppointmentDate,
            Status = AppointmentAttemptStatus.Requested.ToString(),
            BuyerFirstName = reservation.Name,
            BuyerLastName = reservation.LastName,
            BuyerCIN = reservation.CIN,
            BuyerEmail = reservation.Email,
            BuyerPhoneNumber = reservation.PhoneNumber,
            PropertyPrice = reservation.TotalPropertyPrice,
            TaxFees = request.TaxFees,
            TahfidFees = request.TahfidFees,
            CreatedAt = DateTime.UtcNow
        };

        _db.Add(notaryAppointment);

        if (!string.IsNullOrEmpty(request.NotaireId))
        {
            _db.Add(new NotaryAppointmentAssignmentHistory
            {
                Id = Guid.NewGuid(),
                NotaryAppointmentId = notaryAppointment.Id,
                NotaireId = request.NotaireId,
                PreviousNotaireId = null,
                AssignmentSource = "MANUAL",
                ActorUserId = request.ConnectedUserId,
                AssignedAt = notaryAppointment.CreatedAt
            });
        }

        // Increment leads for the agent associated with the reservation.
        if (!string.IsNullOrEmpty(request.AgentId))
        {
            var performanceIndicator = await _db.Set<PerformanceIndicator>()
                .FirstOrDefaultAsync(pi => pi.AgentId == request.AgentId, ct);
            performanceIndicator?.IncrementLeadsGenerated();
        }

        // Tax/tahfid fees are due to third parties (state, conservation foncière),
        // not to GPIA — they are not a ledger payment, only reflected on the
        // purchase shell below (§14.1: GPIA never collects money itself).
        // Purchase.UserId is non-nullable, so this legacy shell only exists for a
        // buyer who has an account. A walk-in recorded on the reservation's own
        // identity fields is legitimate (§1.1) and must not be blocked from
        // reaching the notary — previously this inserted NULL and failed with a
        // bare 500. The dossier itself lives on the reservation, not here.
        var buyerAccountId = !string.IsNullOrWhiteSpace(request.BuyerId)
            ? request.BuyerId
            : reservation.BuyerId;

        if (!string.IsNullOrWhiteSpace(buyerAccountId))
        {
            var purchase = new Purchase
            {
                Id = Guid.NewGuid(),
                UserId = buyerAccountId!,
                ReservationId = request.ReservationId,
                NotaryAppointmentId = notaryAppointment.Id,
                TotalPrice = reservation.TotalPropertyPrice,
                PaidAmount = 0,
                RemainingAmount = reservation.TotalPropertyPrice,
                CreatedAt = DateTime.UtcNow
            };
            _db.Add(purchase);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        await _db.SaveChangesAsync(ct);
        // Recomputes PaidAmount/RemainingAmount from the ledger (§5.8) — the
        // purchase shell above starts at 0 regardless of any prior payments
        // recorded against the reservation, so this reconciles it immediately.
        await _purchaseTotals.RefreshForReservationAsync(request.ReservationId, ct);
        await _db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);

        return new CreateNotaryAppointmentResponse
        {
            Id = notaryAppointment.Id,
            Message = "Notary appointment created successfully."
        };
    }

    /// <summary>
    /// The chosen notary must actually hold an active NOTARY ProjectMembership
    /// on the project the reservation's unit belongs to — a notary is always
    /// manually picked (no auto-assignment strategy, unlike sales agents), but
    /// nothing previously stopped that pick from being someone with no real
    /// standing on the project at all.
    /// </summary>
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
                new FluentValidation.Results.ValidationFailure(nameof(CreateNotaryAppointmentCommand.NotaireId),
                    $"Le notaire {notaryId} n'a pas d'affectation NOTARY active sur ce projet.")
            });
        }
    }

    /// <summary>Rejects a slot that overlaps an active appointment or a block for the same notary (§10.1).</summary>
    private async Task EnsureSlotAvailableAsync(string notaryId, DateTime appointmentDate, CancellationToken ct)
    {
        var blockingStatuses = AppointmentStateMachine.BlockingStatuses.Select(s => s.ToString()).ToArray();

        // Existing appointments are stored as a single instant (AppointmentDate) rather than
        // an interval; treat them, like GetNotaryCalendarHandler does, as occupying one minute.
        var conflictWindowStart = appointmentDate.AddMinutes(-1);
        var conflictWindowEnd = appointmentDate.AddMinutes(1);

        var hasConflict = await _db.Set<NotaryAppointment>().AnyAsync(a =>
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
