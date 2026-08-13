using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Notifications;
using ProjectAPI.Domain.Notifications.Entities;
using ProjectAPI.Domain.Payments.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.BackgroundJobs;

/// <summary>
/// §7 — "installment status refresh (hourly)". PaymentInstallmentStatus is
/// derived on every read (PaymentCalculator.StatusOf), never stored, so there
/// is no stale column to rewrite (§5.8: financial truth is computed, never
/// cached as an independent value). What this job actually does hourly is
/// notify the admin the first time an installment crosses into OVERDUE, since
/// nothing else would ever surface that transition proactively.
/// </summary>
public class InstallmentOverdueJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceProvider _services;
    private readonly ILogger<InstallmentOverdueJob> _logger;

    public InstallmentOverdueJob(IServiceProvider services, ILogger<InstallmentOverdueJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await NotifyNewlyOverdueAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[InstallmentOverdue] Pass failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task NotifyNewlyOverdueAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = DateTime.UtcNow;

        var activeSchedules = await db.Set<PaymentSchedule>()
            .Where(s => s.Status == PaymentScheduleStatus.Active)
            .Include(s => s.Installments)
            .ToListAsync(ct);

        if (activeSchedules.Count == 0) return;

        var reservationIds = activeSchedules.Select(s => s.ReservationId).ToList();

        var reservations = await db.Set<Reservation>()
            .Where(r => reservationIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, ct);

        var alreadyNotified = await db.Set<SentReminder>()
            .Where(s => s.EntityType == "OverdueInstallment")
            .Select(s => s.EntityId)
            .ToListAsync(ct);
        var notifiedSet = alreadyNotified.ToHashSet();

        var notifiedCount = 0;

        foreach (var schedule in activeSchedules)
        {
            var payments = await db.Set<Payment>()
                .Where(p => p.ReservationId == schedule.ReservationId)
                .ToListAsync(ct);

            if (!reservations.TryGetValue(schedule.ReservationId, out var reservation)) continue;

            foreach (var installment in schedule.Installments.Where(i => !i.IsCancelled))
            {
                if (notifiedSet.Contains(installment.Id)) continue;

                var status = PaymentCalculator.StatusOf(installment, payments, now);
                if (status != PaymentInstallmentStatus.Overdue) continue;

                if (!string.IsNullOrWhiteSpace(reservation.OwnerSalesAgentId ?? reservation.AgentId))
                {
                    await notifications.NotifyAsync(
                        reservation.OwnerSalesAgentId ?? reservation.AgentId!,
                        "INSTALLMENT_OVERDUE",
                        "Échéance en retard",
                        $"L'échéance '{installment.LabelFr}' de la réservation {schedule.ReservationId} est en retard.",
                        installment.Id, "PaymentInstallment", ct);
                }

                db.Add(new SentReminder { Id = Guid.NewGuid(), EntityType = "OverdueInstallment", EntityId = installment.Id });
                notifiedCount++;
            }
        }

        if (notifiedCount > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("[InstallmentOverdue] Notified {Count} newly overdue installment(s).", notifiedCount);
        }
    }
}
