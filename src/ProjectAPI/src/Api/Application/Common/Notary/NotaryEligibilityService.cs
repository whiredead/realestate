using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Common.Notary;

/// <summary>
/// §17.6 — the single place notary eligibility is derived, shared by every
/// caller that must (re)check it: appointment creation AND confirmation
/// (§18.3 FR-NOT-002 "recomputed at creation AND confirmation" — snags or the
/// report can change between the two). Previously this logic only lived
/// inline in CreateNotaryAppointmentHandler, so confirming an appointment
/// never re-derived it even though the spec requires both checkpoints.
/// </summary>
public class NotaryEligibilityService
{
    private readonly ApplicationDbContext _db;

    public NotaryEligibilityService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// The §17.6 result on its own, with no title check attached.
    ///
    /// Split out of <see cref="EnsureEligibleAsync"/> because the final-visit
    /// half and the title half gate different things: the sale draft (§6) needs
    /// the visit cleared, but a land title is a precondition of the notarial
    /// act, not of agreeing a price. Folding the two together would have made
    /// a sale undraftable for the entirely unrelated reason that the title
    /// paperwork had not landed yet.
    /// </summary>
    public async Task<NotaryEligibilityResult> CalculateAsync(Guid reservationId, CancellationToken ct)
    {
        var visitCase = await _db.Set<FinalVisitCase>()
            .Include(c => c.Appointments)
            .FirstOrDefaultAsync(c => c.ReservationId == reservationId, ct);

        FinalVisitReport? currentReport = null;
        var snags = new List<Snag>();

        if (visitCase is not null)
        {
            var appointmentIds = visitCase.Appointments.Select(a => a.Id).ToList();

            currentReport = await _db.Set<FinalVisitReport>()
                .Where(r => appointmentIds.Contains(r.AppointmentId) && r.Status != ReportStatus.Superseded)
                .OrderByDescending(r => r.SubmittedAt)
                .ThenByDescending(r => r.VersionNo)
                .FirstOrDefaultAsync(ct);

            if (currentReport is not null)
            {
                snags = await _db.Set<Snag>()
                    .Where(s => s.ReportId == currentReport.Id)
                    .ToListAsync(ct);
            }
        }

        return NotaryEligibilityCalculator.Calculate(visitCase, currentReport, snags);
    }

    /// <summary>
    /// §6 — the final-visit gate alone: the visit is done, its report is
    /// acknowledged, and no blocking or major snag is still active. Used before
    /// a sale may be drafted.
    /// </summary>
    public async Task EnsureFinalVisitClearedAsync(Guid reservationId, CancellationToken ct)
    {
        var eligibility = await CalculateAsync(reservationId, ct);
        if (!eligibility.CanRequestAppointment)
        {
            throw BusinessRuleException.NotaryNotEligible(eligibility.Reasons);
        }
    }

    /// <summary>Throws <see cref="BusinessRuleException"/> when the dossier is not eligible.</summary>
    public async Task EnsureEligibleAsync(Guid reservationId, Guid unitId, CancellationToken ct)
    {
        var eligibility = await CalculateAsync(reservationId, ct);

        var titleState = await _db.Set<UnitTitleState>()
            .FirstOrDefaultAsync(t => t.UnitId == unitId, ct);
        var titleStatus = titleState?.Status ?? TitleStatus.NotAvailable;
        var titleAllows = TitleStateMachine.AllowsNotaryAppointment(titleStatus);

        if (!eligibility.CanRequestAppointment || !titleAllows)
        {
            var reasons = new List<string>(eligibility.Reasons);
            if (!titleAllows)
            {
                reasons.Add($"Le titre foncier doit être disponible, remis au notaire ou complété (statut actuel : {titleStatus}).");
            }
            throw BusinessRuleException.NotaryNotEligible(reasons);
        }
    }
}
