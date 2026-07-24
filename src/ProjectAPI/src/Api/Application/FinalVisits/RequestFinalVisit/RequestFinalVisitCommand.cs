using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.FinalVisits.RequestFinalVisit;

/// <summary>
/// Requests a final visit (spec §17.1 FR-FVI-001).
///
/// Prerequisites: the reservation is approved or converted, the project is
/// COMPLETED, and no active attempt already exists. The case is created once
/// per reservation; a reschedule adds an attempt to the SAME case so history is
/// preserved.
/// </summary>
public class RequestFinalVisitCommand : IRequest<RequestFinalVisitResponse>
{
    public Guid ReservationId { get; set; }

    /// <summary>Requested slot start (half-open interval with EndsAt).</summary>
    public DateTime StartsAt { get; set; }

    /// <summary>Defaults to one hour after the start when omitted.</summary>
    public DateTime? EndsAt { get; set; }

    /// <summary>Set when this attempt follows an unsatisfactory visit (§17.2 FR-FVI-004).</summary>
    public string? CauseType { get; set; }
    public string? CauseDescription { get; set; }
}

public class RequestFinalVisitResponse
{
    public Guid CaseId { get; set; }
    public Guid AppointmentId { get; set; }
    public int AttemptNo { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class RequestFinalVisitHandler : IRequestHandler<RequestFinalVisitCommand, RequestFinalVisitResponse>
{
    private readonly ApplicationDbContext _db;

    public RequestFinalVisitHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<RequestFinalVisitResponse> Handle(RequestFinalVisitCommand request, CancellationToken ct)
    {
        var reservation = await _db.Set<Reservation>()
            .FirstOrDefaultAsync(r => r.Id == request.ReservationId, ct)
            ?? throw new NotFoundException($"Reservation {request.ReservationId} not found.");

        // --- prerequisite 1: the reservation must be approved or converted ---
        if (reservation.Status is not (ReservationStatus.Approved or ReservationStatus.Sold))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.InvalidStatusTransition,
                $"La visite finale exige une réservation approuvée ou convertie (statut actuel : {reservation.Status}).",
                StatusCodes.Status409Conflict);
        }

        // --- prerequisite 2: the project must be COMPLETED (§7.5 / §17.1) ---
        // Unit.ProjectId actually references the Immeuble, which carries the project.
        var project = await (from unit in _db.Set<Domain.Immeubles.Entities.Unit>()
                             join immeuble in _db.Set<Immeuble>() on unit.ProjectId equals immeuble.Id
                             join p in _db.Set<Project>() on immeuble.ProjectId equals p.Id
                             where unit.Id == reservation.UnitId
                             select p).FirstOrDefaultAsync(ct);

        if (project is null)
        {
            throw new NotFoundException($"Project for unit {reservation.UnitId} not found.");
        }

        if (!ProjectStatusCodes.AllowsFinalVisit(project.StatusGlobal))
        {
            throw new BusinessRuleException(
                "PROJECT_NOT_COMPLETED",
                $"La visite finale n'est possible que lorsque le projet est terminé (statut actuel : {project.StatusGlobal}).",
                StatusCodes.Status409Conflict);
        }

        // --- the case: one per reservation, reused across attempts ---
        var visitCase = await _db.Set<FinalVisitCase>()
            .Include(c => c.Appointments)
            .FirstOrDefaultAsync(c => c.ReservationId == request.ReservationId, ct);

        if (visitCase is null)
        {
            visitCase = new FinalVisitCase
            {
                Id = Guid.NewGuid(),
                ReservationId = request.ReservationId,
                Status = FinalVisitCaseStatus.Open,
                ResponsibleSalesAgentId = reservation.AgentId,
                OpenedAt = DateTime.UtcNow
            };
            _db.Add(visitCase);
        }
        else if (visitCase.Appointments.Any(a =>
                     AppointmentStateMachine.BlockingStatuses.Contains(a.Status)))
        {
            // §17.1: no second active attempt while one is still open.
            throw new BusinessRuleException(
                "FINAL_VISIT_ALREADY_ACTIVE",
                "Une demande de visite finale est déjà en cours pour cette réservation.",
                StatusCodes.Status409Conflict);
        }

        var attemptNo = visitCase.Appointments.Count == 0
            ? 1
            : visitCase.Appointments.Max(a => a.AttemptNo) + 1;

        var previous = visitCase.Appointments
            .OrderByDescending(a => a.AttemptNo)
            .FirstOrDefault();

        var appointment = new FinalVisitAppointment
        {
            Id = Guid.NewGuid(),
            CaseId = visitCase.Id,
            AttemptNo = attemptNo,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt ?? request.StartsAt.AddHours(1),
            Status = AppointmentAttemptStatus.Requested,
            PreviousAppointmentId = previous?.Id,
            CauseType = request.CauseType,
            CauseDescription = request.CauseDescription,
            CreatedAt = DateTime.UtcNow
        };

        _db.Add(appointment);
        await _db.SaveChangesAsync(ct);

        return new RequestFinalVisitResponse
        {
            CaseId = visitCase.Id,
            AppointmentId = appointment.Id,
            AttemptNo = attemptNo,
            Message = attemptNo == 1
                ? "Demande de visite finale enregistrée."
                : $"Nouvelle tentative de visite finale (n°{attemptNo}) enregistrée."
        };
    }
}
