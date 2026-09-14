using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.FinalVisits.AcknowledgeReport;

/// <summary>
/// Buyer acknowledgement of a final-visit report (spec §17.3 FR-FVI-007).
///
/// The buyer either confirms having read the report, or disputes it with a
/// reason. This is a simple in-app confirmation and explicitly NOT a certified
/// electronic signature (§4.3 excludes those).
/// </summary>
public class AcknowledgeReportCommand : IRequest<AcknowledgeReportResponse>
{
    public Guid ReportId { get; set; }

    /// <summary>False disputes the report; a reason is then required.</summary>
    public bool Accept { get; set; } = true;

    public string? DisputeReason { get; set; }

    public string? BuyerUserId { get; set; }
}

public class AcknowledgeReportResponse
{
    public Guid ReportId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class AcknowledgeReportHandler : IRequestHandler<AcknowledgeReportCommand, AcknowledgeReportResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;

    public AcknowledgeReportHandler(ApplicationDbContext db, ProjectScopeService projectScope, ICurrentUser currentUser)
    {
        _db = db;
        _projectScope = projectScope;
        _currentUser = currentUser;
    }

    public async Task<AcknowledgeReportResponse> Handle(AcknowledgeReportCommand request, CancellationToken ct)
    {
        var report = await _db.Set<FinalVisitReport>()
            .FirstOrDefaultAsync(r => r.Id == request.ReportId, ct)
            ?? throw new NotFoundException($"Final visit report {request.ReportId} not found.");

        // §17.3 — acknowledgement is the buyer's own decision. It must not
        // be recorded by an agent/admin on their behalf, nor attributed to a
        // caller-supplied id.
        var reservationId = await (
            from a in _db.Set<FinalVisitAppointment>()
            join c in _db.Set<FinalVisitCase>() on a.CaseId equals c.Id
            where a.Id == report.AppointmentId
            select c.ReservationId).FirstOrDefaultAsync(ct);

        if (reservationId == Guid.Empty)
            throw BusinessRuleException.BuyerScopeDenied();

        if (!_currentUser.IsInRole(RoleCodes.Buyer))
            throw BusinessRuleException.BuyerScopeDenied();

        await _projectScope.EnsureBuyerOwnsReservationAsync(reservationId, ct);

        if (report.Status != ReportStatus.AwaitingBuyerAcknowledgement)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.InvalidStatusTransition,
                $"Ce compte rendu n'est pas en attente de validation (statut actuel : {report.Status}).",
                StatusCodes.Status409Conflict);
        }

        if (!request.Accept && string.IsNullOrWhiteSpace(request.DisputeReason))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "Un motif est obligatoire pour contester un compte rendu.");
        }

        if (request.Accept)
        {
            report.Status = ReportStatus.Acknowledged;
            report.AcknowledgedAt = DateTime.UtcNow;
            report.AcknowledgedBy = _currentUser.UserId;
        }
        else
        {
            // A disputed report blocks the notary stage until it is resolved
            // (see NotaryEligibilityCalculator).
            report.Status = ReportStatus.Disputed;
            report.DisputeReason = request.DisputeReason;
        }

        await _db.SaveChangesAsync(ct);

        return new AcknowledgeReportResponse
        {
            ReportId = report.Id,
            Status = report.Status.ToString(),
            Message = request.Accept
                ? "Compte rendu validé par l'acheteur."
                : "Compte rendu contesté. Le passage chez le notaire reste bloqué."
        };
    }
}
