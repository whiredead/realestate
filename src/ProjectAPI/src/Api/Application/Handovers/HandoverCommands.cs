using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Common.Units;
using ProjectAPI.Domain.Crm.Entities;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Domain.Handovers.Entities;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Handovers;

// ---------------------------------------------------------------------------
// Schedule
// ---------------------------------------------------------------------------

public class ScheduleHandoverCommand : IRequest<Guid>
{
    public Guid ReservationId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string? Location { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// §5.8 — a handover may only be planned once the purchase is finalised at the
/// notary. Anything earlier would be handing over keys to a property that has
/// not legally changed hands.
/// </summary>
public class ScheduleHandoverHandler : IRequestHandler<ScheduleHandoverCommand, Guid>
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<ScheduleHandoverHandler> _logger;

    public ScheduleHandoverHandler(ApplicationDbContext db, ICurrentUser currentUser, ILogger<ScheduleHandoverHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Guid> Handle(ScheduleHandoverCommand request, CancellationToken ct)
    {
        var reservation = await _db.Reservations.FirstOrDefaultAsync(r => r.Id == request.ReservationId, ct)
            ?? throw new NotFoundException($"Reservation {request.ReservationId} not found.");

        // The sale must be complete. ReservationStatus.Sold is the spec's
        // CONVERTED, set only by a notary appointment whose outcome was
        // PURCHASE_COMPLETED (§5.7).
        if (reservation.Status != ReservationStatus.Sold)
        {
            throw BusinessRuleException.InvalidStatusTransition(reservation.Status.ToString(), "HANDOVER");
        }

        var unit = await _db.Units.FirstOrDefaultAsync(u => u.Id == reservation.UnitId, ct)
            ?? throw new NotFoundException($"Unit {reservation.UnitId} not found.");

        if (unit.Status == UnitCommercialStatus.Delivered)
        {
            throw BusinessRuleException.InvalidStatusTransition("DELIVERED", "HANDOVER");
        }

        var appointment = new HandoverAppointment
        {
            Id = Guid.NewGuid(),
            ReservationId = reservation.Id,
            UnitId = reservation.UnitId,
            ScheduledAt = request.ScheduledAt,
            Location = request.Location,
            Notes = request.Notes,
            SalesAgentId = reservation.AgentId,
            Status = AppointmentAttemptStatus.Requested,
            CreatedBy = _currentUser.UserId,
        };

        _db.HandoverAppointments.Add(appointment);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("[Handover] Scheduled {Id} for reservation {ReservationId}.", appointment.Id, reservation.Id);
        return appointment.Id;
    }
}

// ---------------------------------------------------------------------------
// Confirm
// ---------------------------------------------------------------------------

public class ConfirmHandoverCommand : IRequest<bool>
{
    public Guid AppointmentId { get; set; }
}

public class ConfirmHandoverHandler : IRequestHandler<ConfirmHandoverCommand, bool>
{
    private readonly ApplicationDbContext _db;

    public ConfirmHandoverHandler(ApplicationDbContext db) => _db = db;

    public async Task<bool> Handle(ConfirmHandoverCommand request, CancellationToken ct)
    {
        var appointment = await _db.HandoverAppointments.FirstOrDefaultAsync(a => a.Id == request.AppointmentId, ct)
            ?? throw new NotFoundException($"Handover appointment {request.AppointmentId} not found.");

        // The shared §47.3 matrix, the same one final-visit and notary
        // appointments obey — no bespoke rules for handovers.
        if (!AppointmentStateMachine.CanTransition(appointment.Status, AppointmentAttemptStatus.Confirmed))
            throw BusinessRuleException.InvalidStatusTransition(appointment.Status.ToString(), "CONFIRMED");
        appointment.Status = AppointmentAttemptStatus.Confirmed;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

// ---------------------------------------------------------------------------
// Submit the procès-verbal
// ---------------------------------------------------------------------------

public class SubmitHandoverReportCommand : IRequest<Guid>
{
    public Guid AppointmentId { get; set; }
    public string? Participants { get; set; }
    public string? Observations { get; set; }
    public List<HandoverItemInput> Items { get; set; } = new();
}

public class HandoverItemInput
{
    public string ItemType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string? Comment { get; set; }
}

/// <summary>
/// §19.3 — the agent records what was handed over. This does NOT deliver the
/// property: the report goes to the buyer for acknowledgement first.
/// </summary>
public class SubmitHandoverReportHandler : IRequestHandler<SubmitHandoverReportCommand, Guid>
{
    private readonly ApplicationDbContext _db;

    public SubmitHandoverReportHandler(ApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(SubmitHandoverReportCommand request, CancellationToken ct)
    {
        var appointment = await _db.HandoverAppointments
            .Include(a => a.Reports)
            .FirstOrDefaultAsync(a => a.Id == request.AppointmentId, ct)
            ?? throw new NotFoundException($"Handover appointment {request.AppointmentId} not found.");

        if (appointment.Status != AppointmentAttemptStatus.Confirmed
            && appointment.Status != AppointmentAttemptStatus.Completed)
        {
            throw BusinessRuleException.InvalidStatusTransition(appointment.Status.ToString(), "HANDOVER_REPORT");
        }

        // A correction supersedes rather than overwrites: the buyer may already
        // have acknowledged the previous version (§19.3).
        var current = appointment.Reports
            .Where(r => r.Status != HandoverReportStatus.Superseded)
            .OrderByDescending(r => r.VersionNo)
            .FirstOrDefault();

        if (current is { Status: HandoverReportStatus.Acknowledged })
        {
            throw BusinessRuleException.InvalidStatusTransition("ACKNOWLEDGED", "HANDOVER_REPORT");
        }

        if (current is not null) current.Status = HandoverReportStatus.Superseded;

        var report = new HandoverReport
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointment.Id,
            VersionNo = (appointment.Reports.Count == 0 ? 0 : appointment.Reports.Max(r => r.VersionNo)) + 1,
            Status = HandoverReportStatus.AwaitingBuyerAcknowledgement,
            Participants = request.Participants,
            Observations = request.Observations,
            SubmittedAt = DateTime.UtcNow,
        };

        foreach (var item in request.Items)
        {
            report.Items.Add(new HandoverItem
            {
                Id = Guid.NewGuid(),
                ItemType = item.ItemType,
                Quantity = item.Quantity,
                Comment = item.Comment,
            });
        }

        _db.HandoverReports.Add(report);

        if (appointment.Status == AppointmentAttemptStatus.Confirmed)
        {
            if (!AppointmentStateMachine.CanTransition(appointment.Status, AppointmentAttemptStatus.Completed))
                throw BusinessRuleException.InvalidStatusTransition(appointment.Status.ToString(), "COMPLETED");
            appointment.Status = AppointmentAttemptStatus.Completed;
        }

        await _db.SaveChangesAsync(ct);
        return report.Id;
    }
}

// ---------------------------------------------------------------------------
// Acknowledge — the step that actually delivers the property
// ---------------------------------------------------------------------------

public class AcknowledgeHandoverCommand : IRequest<bool>
{
    public Guid ReportId { get; set; }

    /// <summary>Warranty length; §20 leaves it configurable, 12 months by default.</summary>
    public int WarrantyMonths { get; set; } = 12;
}

/// <summary>
/// §5.8 — the buyer confirms receipt, and only then:
/// the unit becomes DELIVERED, the delivery date is recorded, the warranty period
/// starts and SAV opens for that unit.
///
/// Idempotent by explicit requirement: "re-validating never restarts the warranty
/// or creates a second delivery". Acknowledging an already-acknowledged report is
/// therefore a no-op that succeeds, not an error — a buyer double-clicking must
/// not extend their own warranty.
/// </summary>
public class AcknowledgeHandoverHandler : IRequestHandler<AcknowledgeHandoverCommand, bool>
{
    private readonly ApplicationDbContext _db;
    private readonly IUnitStatusService _unitStatus;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AcknowledgeHandoverHandler> _logger;

    public AcknowledgeHandoverHandler(
        ApplicationDbContext db,
        IUnitStatusService unitStatus,
        ICurrentUser currentUser,
        ILogger<AcknowledgeHandoverHandler> logger)
    {
        _db = db;
        _unitStatus = unitStatus;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<bool> Handle(AcknowledgeHandoverCommand request, CancellationToken ct)
    {
        var report = await _db.HandoverReports
            .Include(r => r.Appointment)
            .FirstOrDefaultAsync(r => r.Id == request.ReportId, ct)
            ?? throw new NotFoundException($"Handover report {request.ReportId} not found.");

        if (report.Status == HandoverReportStatus.Acknowledged)
        {
            _logger.LogInformation("[Handover] Report {ReportId} already acknowledged; no-op (§5.8 idempotence).", report.Id);
            return true;
        }

        if (report.Status == HandoverReportStatus.Superseded)
        {
            throw BusinessRuleException.InvalidStatusTransition("SUPERSEDED", "ACKNOWLEDGED");
        }

        var appointment = report.Appointment;
        var reservation = await _db.Reservations.FirstOrDefaultAsync(r => r.Id == appointment.ReservationId, ct)
            ?? throw new NotFoundException($"Reservation {appointment.ReservationId} not found.");

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        report.Status = HandoverReportStatus.Acknowledged;
        report.AcknowledgedAt = DateTime.UtcNow;
        report.AcknowledgedByContactId = reservation.PrimaryContactId;

        // The transition itself refuses anything but SOLD → DELIVERED (§3), so a
        // unit that never reached the notary cannot be delivered by this path.
        await _unitStatus.TransitionAsync(
            reservation.UnitId,
            UnitCommercialStatus.Delivered,
            UnitStatusCause.HandoverAcknowledged,
            reservationId: reservation.Id,
            actorUserId: _currentUser.UserId,
            ct: ct);

        // §5.8/§20 — delivery starts the warranty, which is what opens SAV. The
        // filtered unique index is the real guarantee against a second one.
        var alreadyCovered = await _db.Warranties
            .AnyAsync(w => w.UnitId == reservation.UnitId && w.IsActive, ct);

        if (!alreadyCovered)
        {
            var startsAt = DateTime.UtcNow;
            _db.Warranties.Add(new Warranty
            {
                Id = Guid.NewGuid(),
                UnitId = reservation.UnitId,
                ReservationId = reservation.Id,
                WarrantyTypeCode = "GENERAL",
                StartsAt = startsAt,
                EndsAt = startsAt.AddMonths(request.WarrantyMonths <= 0 ? 12 : request.WarrantyMonths),
                IsActive = true,
            });
        }

        // §1.1 — the person has taken delivery. Advances the contact, not the
        // account: a buyer with no login is still an owner.
        if (reservation.PrimaryContactId is not null)
        {
            var contact = await _db.CrmContacts.FirstOrDefaultAsync(c => c.Id == reservation.PrimaryContactId, ct);
            if (contact is not null && contact.LifecycleStatus != ContactLifecycleStatus.DeliveredOwner)
            {
                contact.LifecycleStatus = ContactLifecycleStatus.DeliveredOwner;
                contact.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _logger.LogInformation(
            "[Handover] Unit {UnitId} DELIVERED via report {ReportId}; warranty active, SAV open.",
            reservation.UnitId, report.Id);

        return true;
    }
}
