using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Infrastructure.Context;

// ProjectAPI.Domain.Immeubles.Entities.Unit collides with MediatR.Unit, which the
// global usings pull in. Alias it rather than fully qualifying every signature.
using DomainUnit = ProjectAPI.Domain.Immeubles.Entities.Unit;

namespace ProjectAPI.Api.Application.Common.Units;

/// <summary>
/// The single writer for <c>Unit.Status</c> (spec §3, §7).
///
/// Before this existed, the only code that ever changed a unit's commercial
/// status was the generic <c>UpdateUnit</c> CRUD handler — so approving a
/// reservation left the unit AVAILABLE and a completed sale never marked it
/// SOLD. Every transition now goes through here, which:
///   1. validates the move against <see cref="UnitStateMachine"/>, and
///   2. appends a <see cref="UnitStatusHistory"/> row.
///
/// It deliberately does NOT call SaveChanges: the caller owns the transaction,
/// because §3 and §5.7 require the unit change and the reservation change to
/// commit together or not at all.
/// </summary>
public interface IUnitStatusService
{
    /// <summary>
    /// Moves a unit to <paramref name="target"/>, recording why.
    /// Throws <see cref="NotFoundException"/> if the unit is unknown and
    /// <see cref="InvalidUnitTransitionException"/> if the matrix forbids the move.
    /// </summary>
    Task<DomainUnit> TransitionAsync(
        Guid unitId,
        UnitCommercialStatus target,
        string cause,
        Guid? reservationId = null,
        string? actorUserId = null,
        string? reason = null,
        CancellationToken ct = default);

    /// <summary>
    /// Same, but tolerates a unit already in the target state (returns it
    /// unchanged, writing no history). Used where a command may legitimately run
    /// twice — resubmitting a CHANGES_REQUESTED reservation re-asserts a hold the
    /// unit already has (§5.3: "resubmit; unit stays held").
    /// </summary>
    Task<DomainUnit> TransitionIfNeededAsync(
        Guid unitId,
        UnitCommercialStatus target,
        string cause,
        Guid? reservationId = null,
        string? actorUserId = null,
        string? reason = null,
        CancellationToken ct = default);
}

public class UnitStatusService : IUnitStatusService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<UnitStatusService> _logger;

    public UnitStatusService(ApplicationDbContext db, ILogger<UnitStatusService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DomainUnit> TransitionAsync(
        Guid unitId,
        UnitCommercialStatus target,
        string cause,
        Guid? reservationId = null,
        string? actorUserId = null,
        string? reason = null,
        CancellationToken ct = default)
    {
        var unit = await LoadAsync(unitId, ct);

        UnitStateMachine.EnsureCanTransition(unit.Status, target);
        Apply(unit, target, cause, reservationId, actorUserId, reason);

        return unit;
    }

    public async Task<DomainUnit> TransitionIfNeededAsync(
        Guid unitId,
        UnitCommercialStatus target,
        string cause,
        Guid? reservationId = null,
        string? actorUserId = null,
        string? reason = null,
        CancellationToken ct = default)
    {
        var unit = await LoadAsync(unitId, ct);

        if (unit.Status == target)
        {
            return unit;
        }

        UnitStateMachine.EnsureCanTransition(unit.Status, target);
        Apply(unit, target, cause, reservationId, actorUserId, reason);

        return unit;
    }

    private async Task<DomainUnit> LoadAsync(Guid unitId, CancellationToken ct) =>
        await _db.Units.FirstOrDefaultAsync(u => u.Id == unitId, ct)
            ?? throw new NotFoundException($"Unit with ID '{unitId}' not found.");

    private void Apply(
        DomainUnit unit,
        UnitCommercialStatus target,
        string cause,
        Guid? reservationId,
        string? actorUserId,
        string? reason)
    {
        var from = unit.Status;
        unit.Status = target;

        var history = new UnitStatusHistory
        {
            Id = Guid.NewGuid(),
            UnitId = unit.Id,
            Unit = unit,
            FromStatus = from,
            ToStatus = target,
            Cause = cause,
            ReservationId = reservationId,
            ActorUserId = actorUserId,
            Reason = reason,
            OccurredAt = DateTime.UtcNow
        };

        // Added explicitly to the set rather than only through unit.StatusHistory.
        // Adding to a lazily-unloaded collection navigation leaves EF free to
        // track the row as Modified instead of Added, which produces an UPDATE
        // against a row that does not exist yet — a silent no-op that loses the
        // audit trail. Setting the Unit navigation as well keeps insert ordering
        // correct within the batch.
        _db.Set<UnitStatusHistory>().Add(history);

        _logger.LogInformation(
            "[UnitStatus] {UnitId}: {From} -> {To} ({Cause}, reservation {ReservationId})",
            unit.Id, from.ToCode(), target.ToCode(), cause, reservationId);
    }
}

/// <summary>Cause codes recorded in <see cref="UnitStatusHistory.Cause"/>.</summary>
public static class UnitStatusCause
{
    public const string ReservationSubmitted = "RESERVATION_SUBMITTED";
    public const string ReservationApproved = "RESERVATION_APPROVED";
    public const string ReservationRejected = "RESERVATION_REJECTED";
    public const string ReservationCancelled = "RESERVATION_CANCELLED";
    public const string ReservationExpired = "RESERVATION_EXPIRED";
    public const string NotaryPurchaseCompleted = "NOTARY_PURCHASE_COMPLETED";
    public const string HandoverAcknowledged = "HANDOVER_ACKNOWLEDGED";
    public const string AdminSuspended = "ADMIN_SUSPENDED";
    public const string AdminCancelled = "ADMIN_CANCELLED";
}
