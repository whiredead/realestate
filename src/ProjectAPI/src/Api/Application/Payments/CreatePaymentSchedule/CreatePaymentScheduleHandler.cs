using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Payments.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Payments.CreatePaymentSchedule;

/// <summary>
/// Creates a schedule and, optionally, activates it (spec §14.2).
///
/// Activation rules (FR-PAY-002):
///   - percentages sum to 100 (± 0.01 point);
///   - amounts sum to the contract amount (± 0.01 MAD);
///   - only one ACTIVE schedule per reservation — activating a revision
///     supersedes the previous one instead of rewriting it.
/// </summary>
public class CreatePaymentScheduleHandler
    : IRequestHandler<CreatePaymentScheduleCommand, CreatePaymentScheduleResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public CreatePaymentScheduleHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<CreatePaymentScheduleResponse> Handle(
        CreatePaymentScheduleCommand request,
        CancellationToken ct)
    {
        // §6.4 — schedule creation is admin-only and project-scoped.
        await _projectScope.EnsureReservationAccessAsync(request.ReservationId, ct);

        var reservation = await _db.Set<Reservation>()
            .FirstOrDefaultAsync(r => r.Id == request.ReservationId, ct)
            ?? throw new NotFoundException($"Reservation {request.ReservationId} not found.");

        if (request.Installments.Count == 0)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "Un échéancier doit contenir au moins une échéance.");
        }

        if (request.Installments.Select(i => i.SequenceNo).Distinct().Count() != request.Installments.Count)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "Les numéros d'ordre des échéances doivent être uniques.");
        }

        // Version numbering is per reservation.
        var lastVersion = await _db.Set<PaymentSchedule>()
            .Where(s => s.ReservationId == request.ReservationId)
            .MaxAsync(s => (int?)s.VersionNo, ct) ?? 0;

        var schedule = new PaymentSchedule
        {
            Id = Guid.NewGuid(),
            ReservationId = request.ReservationId,
            VersionNo = lastVersion + 1,
            Status = PaymentScheduleStatus.Draft,
            ContractAmount = request.ContractAmount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "MAD" : request.Currency,
            SupersedesId = request.SupersedesId,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var input in request.Installments)
        {
            schedule.Installments.Add(new PaymentInstallment
            {
                Id = Guid.NewGuid(),
                ScheduleId = schedule.Id,
                SequenceNo = input.SequenceNo,
                LabelFr = input.LabelFr,
                LabelEn = input.LabelEn ?? input.LabelFr,
                Percentage = input.Percentage,
                Amount = input.Amount,
                DueDate = input.DueDate,
                Comment = input.Comment
            });
        }

        // In DRAFT the spec only caps the total at 100 % (§14.2).
        var draftPercent = schedule.Installments.Sum(i => i.Percentage);
        if (draftPercent > 100m + PaymentCalculator.PercentageTolerance)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                $"La somme des pourcentages ne peut pas dépasser 100 % (actuellement {draftPercent:N2} %).");
        }

        _db.Add(schedule);

        var message = "Échéancier créé en brouillon.";

        if (request.ActivateImmediately)
        {
            if (!PaymentCalculator.CanActivate(schedule, out var reason))
            {
                throw new BusinessRuleException(BusinessErrorCodes.ValidationFailed, reason!);
            }

            await SupersedeCurrentActiveAsync(request.ReservationId, ct);

            schedule.Status = PaymentScheduleStatus.Active;
            schedule.ActivatedAt = DateTime.UtcNow;
            message = "Échéancier créé et activé.";
        }

        // Single SaveChanges: schedule, installments and the superseded row all
        // commit together (§5.7).
        await _db.SaveChangesAsync(ct);

        return new CreatePaymentScheduleResponse
        {
            ScheduleId = schedule.Id,
            VersionNo = schedule.VersionNo,
            Status = schedule.Status.ToString(),
            Message = message
        };
    }

    /// <summary>
    /// Marks the reservation's current ACTIVE schedule as SUPERSEDED so the
    /// filtered unique index never sees two active rows.
    /// </summary>
    private async Task SupersedeCurrentActiveAsync(Guid reservationId, CancellationToken ct)
    {
        var current = await _db.Set<PaymentSchedule>()
            .Where(s => s.ReservationId == reservationId && s.Status == PaymentScheduleStatus.Active)
            .ToListAsync(ct);

        foreach (var existing in current)
        {
            existing.Status = PaymentScheduleStatus.Superseded;
        }
    }
}
