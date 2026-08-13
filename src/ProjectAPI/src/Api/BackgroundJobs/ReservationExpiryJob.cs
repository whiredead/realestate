using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Notifications;
using ProjectAPI.Api.Application.Common.Units;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.BackgroundJobs;

/// <summary>
/// Expires overdue reservations and releases their units (spec §12.3, §49.5).
///
/// Runs every 5 minutes as prescribed. Each pass is idempotent: it only touches
/// rows whose ExpiresAt is in the past and whose status still blocks the unit,
/// so re-running it changes nothing once a batch has been processed.
///
/// A reservation with no ExpiresAt never expires — expiry is opt-in per file.
/// </summary>
public class ReservationExpiryJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceProvider _services;
    private readonly ILogger<ReservationExpiryJob> _logger;

    public ReservationExpiryJob(IServiceProvider services, ILogger<ReservationExpiryJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small initial delay so startup is not competing with request warm-up.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireDueReservationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // Never let a bad pass kill the loop — log and retry next tick.
                _logger.LogError(ex, "[ReservationExpiry] Pass failed.");
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

    private async Task ExpireDueReservationsAsync(CancellationToken ct)
    {
        // BackgroundService is a singleton; the DbContext is scoped.
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var unitStatus = scope.ServiceProvider.GetRequiredService<IUnitStatusService>();

        var now = DateTime.UtcNow;

        var due = await db.Set<Reservation>()
            .Where(r => r.ExpiresAt != null
                        && r.ExpiresAt < now
                        && (r.Status == ReservationStatus.Pending
                            || r.Status == ReservationStatus.ChangesRequested))
            .ToListAsync(ct);

        if (due.Count == 0) return;

        foreach (var reservation in due)
        {
            // Go through the matrix so the job cannot bypass the lifecycle rules.
            ReservationStateMachine.EnsureCanTransition(reservation.Status, ReservationStatus.Expired);
            reservation.Status = ReservationStatus.Expired;

            // Was flipping the reservation to Expired without ever releasing
            // the unit — UnitCommercialStatus stayed at HoldPendingApproval
            // forever, permanently stranding the unit off-market. Mirrors
            // RejectReservationHandler/CancelReservationHandler's own release
            // call, which this job never had despite its own header comment
            // promising it ("Expires overdue reservations and releases their units").
            await unitStatus.TransitionAsync(
                reservation.UnitId,
                UnitCommercialStatus.Available,
                UnitStatusCause.ReservationExpired,
                reservationId: reservation.Id,
                ct: ct);
        }

        await db.SaveChangesAsync(ct);

        // §6.2 — notify the owning agent so they know the file was released,
        // not just the buyer (who may have no account, per §1.1).
        foreach (var reservation in due)
        {
            if (!string.IsNullOrWhiteSpace(reservation.AgentId))
            {
                await notifications.NotifyAsync(
                    reservation.AgentId,
                    "RESERVATION_EXPIRED",
                    "Réservation expirée",
                    $"La réservation {reservation.Id} a expiré et le bien a été libéré.",
                    reservation.Id, "Reservation", ct);
            }
        }

        _logger.LogInformation("[ReservationExpiry] Expired {Count} reservation(s).", due.Count);
    }
}
