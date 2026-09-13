using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Notary;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Infrastructure.Context;

using UnitEntity = ProjectAPI.Domain.Immeubles.Entities.Unit;

namespace ProjectAPI.Api.Application.Sales.SaleDrafts;

/// <summary>
/// Whether "Créer une vente" may be offered on a reservation, and if not why.
///
/// Same preconditions as <see cref="CreateSaleDraftHandler"/> (approved
/// reservation, project in delivery, final visit cleared, no active sale), read
/// without side effects so the console shows the action only when the API would
/// accept it — the button used to be offered on any approved file and fail on
/// click.
/// </summary>
public class GetSaleEligibilityQuery : IRequest<SaleEligibilityResponse>
{
    public Guid ReservationId { get; set; }
}

public class SaleEligibilityResponse
{
    public bool CanCreate { get; set; }
    public List<string> Reasons { get; set; } = new();
}

public class GetSaleEligibilityHandler : IRequestHandler<GetSaleEligibilityQuery, SaleEligibilityResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;
    private readonly NotaryEligibilityService _eligibility;

    public GetSaleEligibilityHandler(ApplicationDbContext db, ProjectScopeService projectScope, NotaryEligibilityService eligibility)
    {
        _db = db;
        _projectScope = projectScope;
        _eligibility = eligibility;
    }

    public async Task<SaleEligibilityResponse> Handle(GetSaleEligibilityQuery request, CancellationToken ct)
    {
        var reservation = await _db.Set<Reservation>().FirstOrDefaultAsync(r => r.Id == request.ReservationId, ct)
            ?? throw new NotFoundException($"Reservation {request.ReservationId} not found.");

        await _projectScope.EnsureReservationAccessAsync(request.ReservationId, ct);

        var reasons = new List<string>();

        if (reservation.Status != ReservationStatus.Approved)
        {
            reasons.Add($"La réservation doit être approuvée (statut actuel : {reservation.Status}).");
        }

        var project = await (
            from u in _db.Set<UnitEntity>()
            join im in _db.Set<Immeuble>() on u.ProjectId equals im.Id
            join p in _db.Set<Project>() on im.ProjectId equals p.Id
            where u.Id == reservation.UnitId
            select p).FirstOrDefaultAsync(ct);

        var phase = ProjectStatusCodes.GetBusinessPhase(project?.StatusGlobal);
        if (phase != ProjectStatusCodes.Phase.EnLivraison)
        {
            reasons.Add($"Le projet doit être en livraison (phase actuelle : {phase}).");
        }

        var visit = await _eligibility.CalculateAsync(request.ReservationId, ct);
        if (!visit.CanRequestAppointment)
        {
            reasons.Add("La visite finale doit être validée.");
        }

        var active = SaleStateMachine.ActiveStatuses;
        if (await _db.Set<Sale>().AnyAsync(s => (s.ReservationId == reservation.Id || s.UnitId == reservation.UnitId) && active.Contains(s.Status), ct))
        {
            reasons.Add("Une vente active ou finalisée existe déjà pour ce dossier ou ce bien.");
        }

        return new SaleEligibilityResponse { CanCreate = reasons.Count == 0, Reasons = reasons };
    }
}
