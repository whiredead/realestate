using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Notifications;
using ProjectAPI.Domain.Notifications.Entities;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.BackgroundJobs;

/// <summary>
/// §7/§20 — SAV SLA detection, every 15 minutes. Notifies the assigned
/// technician (and, on breach, the project admin) once a claim's
/// SlaTargetAt is approaching or passed, for claims still ACTIVE
/// (ClaimStateMachine.ActiveStatuses) — a claim already RESOLVED/CLOSED/
/// REJECTED/CANCELLED has no SLA left to breach.
/// </summary>
public class SavSlaDetectionJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan WarningWindow = TimeSpan.FromHours(24);

    private readonly IServiceProvider _services;
    private readonly ILogger<SavSlaDetectionJob> _logger;

    public SavSlaDetectionJob(IServiceProvider services, ILogger<SavSlaDetectionJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DetectSlaEventsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SavSlaDetection] Pass failed.");
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

    private async Task DetectSlaEventsAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = DateTime.UtcNow;
        var warningHorizon = now.Add(WarningWindow);

        var claims = await db.Set<AfterSaleClaim>()
            .Where(c => c.SlaTargetAt != null
                        && ClaimStateMachine.ActiveStatuses.Contains(c.Status)
                        && c.SlaTargetAt <= warningHorizon)
            .ToListAsync(ct);

        if (claims.Count == 0) return;

        var alreadyWarned = await db.Set<SentReminder>()
            .Where(s => s.EntityType == "ClaimSlaWarning" || s.EntityType == "ClaimSlaBreached")
            .Select(s => new { s.EntityType, s.EntityId })
            .ToListAsync(ct);
        var warnedSet = alreadyWarned.Where(s => s.EntityType == "ClaimSlaWarning").Select(s => s.EntityId).ToHashSet();
        var breachedSet = alreadyWarned.Where(s => s.EntityType == "ClaimSlaBreached").Select(s => s.EntityId).ToHashSet();

        var notifiedCount = 0;

        foreach (var claim in claims)
        {
            var breached = claim.SlaTargetAt <= now;

            if (breached && !breachedSet.Contains(claim.Id))
            {
                if (!string.IsNullOrWhiteSpace(claim.AssignedAgentId))
                {
                    await notifications.NotifyAsync(claim.AssignedAgentId, "SAV_SLA_BREACHED",
                        "Délai SAV dépassé", $"La réclamation '{claim.Title}' a dépassé son délai de traitement.",
                        claim.Id, "AfterSaleClaim", ct);
                }

                db.Add(new SentReminder { Id = Guid.NewGuid(), EntityType = "ClaimSlaBreached", EntityId = claim.Id });
                notifiedCount++;
            }
            else if (!breached && !warnedSet.Contains(claim.Id))
            {
                if (!string.IsNullOrWhiteSpace(claim.AssignedAgentId))
                {
                    await notifications.NotifyAsync(claim.AssignedAgentId, "SAV_SLA_WARNING",
                        "Délai SAV proche", $"La réclamation '{claim.Title}' approche de son délai de traitement.",
                        claim.Id, "AfterSaleClaim", ct);
                }

                db.Add(new SentReminder { Id = Guid.NewGuid(), EntityType = "ClaimSlaWarning", EntityId = claim.Id });
                notifiedCount++;
            }
        }

        if (notifiedCount > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("[SavSlaDetection] Sent {Count} SLA notification(s).", notifiedCount);
        }
    }
}
