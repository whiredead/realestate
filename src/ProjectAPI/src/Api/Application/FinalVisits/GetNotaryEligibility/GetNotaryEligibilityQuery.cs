using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.FinalVisits.GetNotaryEligibility;

/// <summary>
/// Returns whether a reservation may proceed to the notary (spec §17.6).
///
/// §17.6 states the API returns the result and its causes, and that the frontend
/// does NOT recompute eligibility — so this is the single place the rule lives.
/// </summary>
public class GetNotaryEligibilityQuery : IRequest<NotaryEligibilityResponse>
{
    public Guid ReservationId { get; set; }
}

public class NotaryEligibilityResponse
{
    public Guid ReservationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool CanRequestAppointment { get; set; }
    public List<string> Reasons { get; set; } = new();

    public int ActiveMinorSnags { get; set; }
    public int ActiveMajorSnags { get; set; }
    public int ActiveBlockingSnags { get; set; }

    /// <summary>Title status — an independent prerequisite (§18.3 FR-NOT-002).</summary>
    public string TitleStatus { get; set; } = string.Empty;
    public bool TitleAllowsAppointment { get; set; }

    public DateTime CalculatedAt { get; set; }
}

public class GetNotaryEligibilityHandler
    : IRequestHandler<GetNotaryEligibilityQuery, NotaryEligibilityResponse>
{
    private readonly ApplicationDbContext _db;

    public GetNotaryEligibilityHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<NotaryEligibilityResponse> Handle(GetNotaryEligibilityQuery request, CancellationToken ct)
    {
        var reservation = await _db.Set<Reservation>()
            .FirstOrDefaultAsync(r => r.Id == request.ReservationId, ct)
            ?? throw new NotFoundException($"Reservation {request.ReservationId} not found.");

        var visitCase = await _db.Set<FinalVisitCase>()
            .Include(c => c.Appointments)
            .FirstOrDefaultAsync(c => c.ReservationId == request.ReservationId, ct);

        FinalVisitReport? currentReport = null;
        var snags = new List<Snag>();

        if (visitCase is not null)
        {
            var appointmentIds = visitCase.Appointments.Select(a => a.Id).ToList();

            // The current report is the latest non-superseded version.
            currentReport = await _db.Set<FinalVisitReport>()
                .Where(r => appointmentIds.Contains(r.AppointmentId) && r.Status != ReportStatus.Superseded)
                .OrderByDescending(r => r.VersionNo)
                .FirstOrDefaultAsync(ct);

            if (currentReport is not null)
            {
                snags = await _db.Set<Snag>()
                    .Where(s => s.ReportId == currentReport.Id)
                    .ToListAsync(ct);
            }
        }

        var eligibility = NotaryEligibilityCalculator.Calculate(visitCase, currentReport, snags);

        // The title is a separate prerequisite (§18.3), reported alongside the
        // snag-based status rather than folded into it.
        var titleState = await _db.Set<UnitTitleState>()
            .FirstOrDefaultAsync(t => t.UnitId == reservation.UnitId, ct);

        var titleStatus = titleState?.Status ?? Domain.Construction.Entities.TitleStatus.NotAvailable;
        var titleAllows = TitleStateMachine.AllowsNotaryAppointment(titleStatus);

        var response = new NotaryEligibilityResponse
        {
            ReservationId = request.ReservationId,
            Status = eligibility.Status.ToString(),
            CanRequestAppointment = eligibility.CanRequestAppointment && titleAllows,
            Reasons = eligibility.Reasons,
            ActiveMinorSnags = eligibility.ActiveMinorSnags,
            ActiveMajorSnags = eligibility.ActiveMajorSnags,
            ActiveBlockingSnags = eligibility.ActiveBlockingSnags,
            TitleStatus = titleStatus.ToString(),
            TitleAllowsAppointment = titleAllows,
            CalculatedAt = eligibility.CalculatedAt
        };

        if (!titleAllows)
        {
            response.Reasons.Add(
                $"Le titre foncier doit être disponible, remis au notaire ou complété (statut actuel : {titleStatus}).");
        }

        return response;
    }
}
