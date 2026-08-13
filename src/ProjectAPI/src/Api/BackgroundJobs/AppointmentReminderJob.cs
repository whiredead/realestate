using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Notifications;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Domain.Notifications.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Infrastructure.Context;
using AppointmentEntity = ProjectAPI.Domain.Appointments.Entities.Appointment;
using HandoverAppointmentEntity = ProjectAPI.Domain.Handovers.Entities.HandoverAppointment;

namespace ProjectAPI.Api.BackgroundJobs;

/// <summary>
/// §7 — appointment reminders, every 5 minutes. Notifies the parties on a
/// CONFIRMED appointment (commercial, final-visit, notary, handover) once it
/// falls inside a 24-hour lead window, and only once per appointment
/// (SentReminder is the dedup marker — see its doc comment).
/// </summary>
public class AppointmentReminderJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LeadWindow = TimeSpan.FromHours(24);

    private readonly IServiceProvider _services;
    private readonly ILogger<AppointmentReminderJob> _logger;

    public AppointmentReminderJob(IServiceProvider services, ILogger<AppointmentReminderJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(35), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendDueRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AppointmentReminder] Pass failed.");
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

    private async Task SendDueRemindersAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = DateTime.UtcNow;
        var horizon = now.Add(LeadWindow);

        var alreadySent = await db.Set<SentReminder>()
            .Where(s => s.EntityType == "CommercialAppointment"
                     || s.EntityType == "FinalVisitAppointment"
                     || s.EntityType == "NotaryAppointment"
                     || s.EntityType == "HandoverAppointment")
            .Select(s => new { s.EntityType, s.EntityId })
            .ToListAsync(ct);
        var sentSet = alreadySent.Select(s => (s.EntityType, s.EntityId)).ToHashSet();

        var sentCount = 0;

        // --- Commercial appointments ---
        var confirmedStatus = AppointmentAttemptStatus.Confirmed.ToString();
        var commercial = await db.Set<AppointmentEntity>()
            .Where(a => a.Status == confirmedStatus && a.AppointmentDate > now && a.AppointmentDate <= horizon)
            .ToListAsync(ct);

        foreach (var a in commercial)
        {
            if (sentSet.Contains(("CommercialAppointment", a.Id))) continue;

            if (!string.IsNullOrWhiteSpace(a.SalesAgentId))
            {
                await notifications.NotifyAsync(a.SalesAgentId, "APPOINTMENT_REMINDER",
                    "Rappel de rendez-vous", $"Rendez-vous commercial le {a.AppointmentDate:g}.",
                    a.Id, "Appointment", ct);
            }
            if (!string.IsNullOrWhiteSpace(a.UserId))
            {
                await notifications.NotifyAsync(a.UserId, "APPOINTMENT_REMINDER",
                    "Rappel de rendez-vous", $"Votre rendez-vous est prévu le {a.AppointmentDate:g}.",
                    a.Id, "Appointment", ct);
            }

            db.Add(new SentReminder { Id = Guid.NewGuid(), EntityType = "CommercialAppointment", EntityId = a.Id });
            sentCount++;
        }

        // --- Final-visit appointments (buyer via Case -> Reservation) ---
        var finalVisits = await (
            from fa in db.Set<FinalVisitAppointment>()
            join c in db.Set<FinalVisitCase>() on fa.CaseId equals c.Id
            join r in db.Set<Reservation>() on c.ReservationId equals r.Id
            where fa.Status == AppointmentAttemptStatus.Confirmed
                  && fa.StartsAt > now && fa.StartsAt <= horizon
            select new { fa.Id, fa.StartsAt, c.ResponsibleSalesAgentId, r.BuyerId }
        ).ToListAsync(ct);

        foreach (var fv in finalVisits)
        {
            if (sentSet.Contains(("FinalVisitAppointment", fv.Id))) continue;

            if (!string.IsNullOrWhiteSpace(fv.ResponsibleSalesAgentId))
            {
                await notifications.NotifyAsync(fv.ResponsibleSalesAgentId, "APPOINTMENT_REMINDER",
                    "Rappel de visite finale", $"Visite finale le {fv.StartsAt:g}.",
                    fv.Id, "FinalVisitAppointment", ct);
            }
            if (!string.IsNullOrWhiteSpace(fv.BuyerId))
            {
                await notifications.NotifyAsync(fv.BuyerId, "APPOINTMENT_REMINDER",
                    "Rappel de visite finale", $"Votre visite finale est prévue le {fv.StartsAt:g}.",
                    fv.Id, "FinalVisitAppointment", ct);
            }

            db.Add(new SentReminder { Id = Guid.NewGuid(), EntityType = "FinalVisitAppointment", EntityId = fv.Id });
            sentCount++;
        }

        // --- Notary appointments ---
        var notaryConfirmed = AppointmentAttemptStatus.Confirmed.ToString();
        var notary = await db.Set<NotaryAppointment>()
            .Where(n => n.Status == notaryConfirmed && n.AppointmentDate > now && n.AppointmentDate <= horizon)
            .ToListAsync(ct);

        foreach (var n in notary)
        {
            if (sentSet.Contains(("NotaryAppointment", n.Id))) continue;

            foreach (var recipient in new[] { n.AgentId, n.BuyerId, n.NotaireId })
            {
                if (!string.IsNullOrWhiteSpace(recipient))
                {
                    await notifications.NotifyAsync(recipient, "APPOINTMENT_REMINDER",
                        "Rappel de rendez-vous notarial", $"Rendez-vous chez le notaire le {n.AppointmentDate:g}.",
                        n.Id, "NotaryAppointment", ct);
                }
            }

            db.Add(new SentReminder { Id = Guid.NewGuid(), EntityType = "NotaryAppointment", EntityId = n.Id });
            sentCount++;
        }

        // --- Handover appointments (buyer via Reservation) ---
        var handovers = await (
            from h in db.Set<HandoverAppointmentEntity>()
            join r in db.Set<Reservation>() on h.ReservationId equals r.Id
            where h.Status == AppointmentAttemptStatus.Confirmed
                  && h.ScheduledAt > now && h.ScheduledAt <= horizon
            select new { h.Id, h.ScheduledAt, h.SalesAgentId, r.BuyerId }
        ).ToListAsync(ct);

        foreach (var h in handovers)
        {
            if (sentSet.Contains(("HandoverAppointment", h.Id))) continue;

            if (!string.IsNullOrWhiteSpace(h.SalesAgentId))
            {
                await notifications.NotifyAsync(h.SalesAgentId, "APPOINTMENT_REMINDER",
                    "Rappel de livraison", $"Remise des clés le {h.ScheduledAt:g}.",
                    h.Id, "HandoverAppointment", ct);
            }
            if (!string.IsNullOrWhiteSpace(h.BuyerId))
            {
                await notifications.NotifyAsync(h.BuyerId, "APPOINTMENT_REMINDER",
                    "Rappel de livraison", $"Votre remise des clés est prévue le {h.ScheduledAt:g}.",
                    h.Id, "HandoverAppointment", ct);
            }

            db.Add(new SentReminder { Id = Guid.NewGuid(), EntityType = "HandoverAppointment", EntityId = h.Id });
            sentCount++;
        }

        if (sentCount > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("[AppointmentReminder] Sent {Count} reminder(s).", sentCount);
        }
    }
}
