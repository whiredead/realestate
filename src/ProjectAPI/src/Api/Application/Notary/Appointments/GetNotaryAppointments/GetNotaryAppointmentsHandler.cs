using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Appointments.Entities;
using ProjectAPI.Domain.Appointments.Interfaces;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Notary.Appointments.GetNotaryAppointments;

/// <summary>
/// §6.4 — the controller only requires "signed in", so the perimeter is
/// enforced here: an internal role only sees appointments for reservations
/// in their assigned projects, and a buyer only ever sees their own.
/// </summary>
public class GetNotaryAppointmentsHandler : IRequestHandler<GetNotaryAppointmentsQuery, PaginatedResponse<NotaryAppointmentResponse>>
{
    private readonly INotaryAppointmentRepository _repository;
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _user;

    public GetNotaryAppointmentsHandler(
        INotaryAppointmentRepository repository,
        ApplicationDbContext db,
        ProjectScopeService projectScope,
        ICurrentUser user)
    {
        _repository = repository;
        _db = db;
        _projectScope = projectScope;
        _user = user;
    }

    public async Task<PaginatedResponse<NotaryAppointmentResponse>> Handle(GetNotaryAppointmentsQuery request, CancellationToken cancellationToken)
    {
        var appointments = (await _repository.Find(
            na => (request.ReservationId == null || na.ReservationId == request.ReservationId) &&
                  (string.IsNullOrEmpty(request.BuyerCIN) || na.BuyerCIN == request.BuyerCIN) &&
                  (string.IsNullOrEmpty(request.NotaireId) || na.NotaireId == request.NotaireId) &&
                  (string.IsNullOrEmpty(request.AgentId) || na.AgentId == request.AgentId) &&
                  (string.IsNullOrEmpty(request.Status) || na.Status == request.Status)
        )).ToList();

        appointments = await ApplyScopeAsync(appointments, cancellationToken);

        var totalItems = appointments.Count();

        var page = appointments
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        // Bypasses UserManager/TPH materialization deliberately — a notary row
        // with a stale Discriminator would throw there (see AlignUserDiscriminator);
        // a bare column read never touches the discriminator at all.
        var notaireIds = page.Select(a => a.NotaireId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        var notaireNames = await _db.Set<User>()
            .Where(u => notaireIds.Contains(u.Id))
            .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName })
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var paginatedData = page
            .Select(na => new NotaryAppointmentResponse
            {
                Id = na.Id,
                BuyerId = na.BuyerId,
                NotaireId = na.NotaireId,
                NotaireFullName = na.NotaireId != null && notaireNames.TryGetValue(na.NotaireId, out var name) ? name : null,
                AgentId = na.AgentId,
                ReservationId = na.ReservationId,
                AppointmentDate = na.AppointmentDate,
                Status = na.Status,
                BuyerFirstName = na.BuyerFirstName,
                BuyerLastName = na.BuyerLastName,
                BuyerCIN = na.BuyerCIN,
                BuyerEmail = na.BuyerEmail,
                BuyerPhoneNumber = na.BuyerPhoneNumber,
                PropertyPrice = na.PropertyPrice,
                TaxFees = na.TaxFees,
                TahfidFees = na.TahfidFees,
                Outcome = na.Outcome?.ToCode(),
                OutcomeNote = na.OutcomeNote
            })
            .ToList();

        return new PaginatedResponse<NotaryAppointmentResponse>(paginatedData, request.PageNumber, request.PageSize, totalItems);
    }

    /// <summary>
    /// Restricts the already-filtered list to what the caller may see (§6.4):
    /// a buyer-only caller keeps just their own reservations' appointments;
    /// an internal role keeps only appointments whose reservation resolves to
    /// a project inside their assignment (GLOBAL_ADMIN sees everything).
    /// </summary>
    private async Task<List<NotaryAppointment>> ApplyScopeAsync(List<NotaryAppointment> appointments, CancellationToken ct)
    {
        if (appointments.Count == 0) return appointments;

        var isBuyerOnly = !_user.Roles.Any(r => RoleCodes.Internal.Contains(r, StringComparer.Ordinal));
        if (isBuyerOnly)
        {
            return appointments.Where(a => a.BuyerId == _user.UserId).ToList();
        }

        // Project scope alone let any NOTARY on a project see (and, per
        // UpdateNotaryAppointmentHandler pre-fix, act on) every other
        // notary's appointments on that same project — no "this is mine"
        // boundary at all, unlike commercial Appointments.
        if (_user.IsInRole(RoleCodes.Notary))
        {
            appointments = appointments.Where(a => a.NotaireId == _user.UserId).ToList();
        }

        var scopedProjectIds = await _projectScope.GetScopedProjectIdsAsync(ct);
        if (scopedProjectIds is null) return appointments; // GLOBAL_ADMIN — unrestricted.

        var reservationIds = appointments.Select(a => a.ReservationId).Distinct().ToList();

        var reservationProjectIds = await (
            from r in _db.Set<Reservation>()
            join u in _db.Set<Domain.Immeubles.Entities.Unit>() on r.UnitId equals u.Id
            join im in _db.Set<Domain.Immeubles.Entities.Immeuble>() on u.ProjectId equals im.Id
            where reservationIds.Contains(r.Id)
            select new { r.Id, ProjectId = im.ProjectId }
        ).ToDictionaryAsync(x => x.Id, x => x.ProjectId, ct);

        return appointments
            .Where(a => reservationProjectIds.TryGetValue(a.ReservationId, out var pid) && scopedProjectIds.Contains(pid))
            .ToList();
    }
}

