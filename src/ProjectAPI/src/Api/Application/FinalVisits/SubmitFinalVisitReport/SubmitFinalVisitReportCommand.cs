using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.FinalVisits.SubmitFinalVisitReport;

/// <summary>
/// Submits the report for a completed final visit, together with its snags
/// (spec §17.3 FR-FVI-005/006).
///
/// On submission the report moves to AWAITING_BUYER_ACKNOWLEDGEMENT: the buyer
/// must confirm it before the notary stage opens (§17.3 FR-FVI-007).
/// </summary>
public class SubmitFinalVisitReportCommand : IRequest<SubmitFinalVisitReportResponse>
{
    public Guid AppointmentId { get; set; }

    /// <summary>CompliantNoSnag, CompliantMinorSnags, NonCompliantMajorSnags, NonCompliantBlockingSnags.</summary>
    public VisitResult ResultCode { get; set; }

    public string? GeneralCondition { get; set; }
    public string? Observations { get; set; }
    public string? AuthorUserId { get; set; }

    public List<SnagInput> Snags { get; set; } = new();

    public class SnagInput
    {
        public string Description { get; set; } = string.Empty;
        public SnagSeverity Severity { get; set; } = SnagSeverity.Minor;
        public string? CategoryCode { get; set; }
        public string? Location { get; set; }
        public DateTime? TargetResolutionDate { get; set; }
    }
}

public class SubmitFinalVisitReportResponse
{
    public Guid ReportId { get; set; }
    public int VersionNo { get; set; }
    public string Status { get; set; } = string.Empty;
    public int SnagCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class SubmitFinalVisitReportHandler
    : IRequestHandler<SubmitFinalVisitReportCommand, SubmitFinalVisitReportResponse>
{
    private readonly ApplicationDbContext _db;

    public SubmitFinalVisitReportHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<SubmitFinalVisitReportResponse> Handle(
        SubmitFinalVisitReportCommand request,
        CancellationToken ct)
    {
        var appointment = await _db.Set<FinalVisitAppointment>()
            .FirstOrDefaultAsync(a => a.Id == request.AppointmentId, ct)
            ?? throw new NotFoundException($"Final visit appointment {request.AppointmentId} not found.");

        // §17.3: a report only exists for a visit that actually took place.
        if (appointment.Status != AppointmentAttemptStatus.Completed)
        {
            throw new BusinessRuleException(
                "APPOINTMENT_NOT_COMPLETED",
                $"Le compte rendu exige une visite réalisée (statut actuel : {appointment.Status}).",
                StatusCodes.Status409Conflict);
        }

        // Declared result and recorded snags must agree, otherwise eligibility
        // would be computed from a contradictory report.
        var maxSeverity = request.Snags.Count == 0
            ? (SnagSeverity?)null
            : request.Snags.Max(s => s.Severity);

        var expected = maxSeverity switch
        {
            null => VisitResult.CompliantNoSnag,
            SnagSeverity.Minor => VisitResult.CompliantMinorSnags,
            SnagSeverity.Major => VisitResult.NonCompliantMajorSnags,
            SnagSeverity.Blocking => VisitResult.NonCompliantBlockingSnags,
            _ => VisitResult.CompliantNoSnag
        };

        if (request.ResultCode != expected)
        {
            throw new BusinessRuleException(
                "INVALID_REPORT_RESULT",
                $"Le résultat déclaré ({request.ResultCode}) ne correspond pas aux réserves saisies (attendu : {expected}).");
        }

        // A new report supersedes the previous version rather than replacing it
        // in place (§17.3): the buyer may already have seen the earlier one.
        var previous = await _db.Set<FinalVisitReport>()
            .Where(r => r.AppointmentId == request.AppointmentId && r.Status != ReportStatus.Superseded)
            .OrderByDescending(r => r.VersionNo)
            .FirstOrDefaultAsync(ct);

        if (previous is not null)
        {
            previous.Status = ReportStatus.Superseded;
        }

        var report = new FinalVisitReport
        {
            Id = Guid.NewGuid(),
            AppointmentId = request.AppointmentId,
            VersionNo = (previous?.VersionNo ?? 0) + 1,
            Status = ReportStatus.AwaitingBuyerAcknowledgement,
            ResultCode = request.ResultCode,
            GeneralCondition = request.GeneralCondition,
            Observations = request.Observations,
            SubmittedAt = DateTime.UtcNow,
            AuthorUserId = request.AuthorUserId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Add(report);

        var visitCase = await _db.Set<FinalVisitCase>()
            .FirstOrDefaultAsync(c => c.Id == appointment.CaseId, ct);

        var sequence = 1;
        foreach (var input in request.Snags)
        {
            var snag = new Snag
            {
                Id = Guid.NewGuid(),
                ReportId = report.Id,
                // Unique, human-readable and traceable back to the report version.
                Code = $"SNG-{report.Id.ToString()[..8].ToUpperInvariant()}-{sequence:D3}",
                CategoryCode = input.CategoryCode,
                Severity = input.Severity,
                Description = input.Description,
                Location = input.Location,
                TargetResolutionDate = input.TargetResolutionDate,
                Status = SnagStatus.Open,
                // §17.4 FR-FVI-010: snags belong to the SALES agent, never to the
                // after-sales technician.
                ResponsibleSalesAgentId = visitCase?.ResponsibleSalesAgentId,
                CreatedAt = DateTime.UtcNow
            };

            _db.Add(snag);
            _db.Add(new SnagHistory
            {
                Id = Guid.NewGuid(),
                // Set via navigation, not just the SnagId scalar, so EF Core
                // orders this insert after its parent Snag row in the same batch.
                Snag = snag,
                FromStatus = null,
                ToStatus = SnagStatus.Open,
                ActorUserId = request.AuthorUserId,
                OccurredAt = DateTime.UtcNow,
                Comment = "Réserve créée lors de la visite finale."
            });

            sequence++;
        }

        // Reflect the outcome on the dossier.
        if (visitCase is not null)
        {
            visitCase.Status = request.ResultCode switch
            {
                VisitResult.CompliantNoSnag => FinalVisitCaseStatus.ReadyForNotary,
                VisitResult.CompliantMinorSnags => FinalVisitCaseStatus.ReadyForNotary,
                _ => FinalVisitCaseStatus.RevisitRequired
            };
        }

        await _db.SaveChangesAsync(ct);

        return new SubmitFinalVisitReportResponse
        {
            ReportId = report.Id,
            VersionNo = report.VersionNo,
            Status = report.Status.ToString(),
            SnagCount = request.Snags.Count,
            Message = "Compte rendu soumis. En attente de validation par l'acheteur."
        };
    }
}
