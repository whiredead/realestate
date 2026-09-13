using ProjectAPI.Api.Application.Common.Units;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Reservations.Interface;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;
using UnitEntity = ProjectAPI.Domain.Immeubles.Entities.Unit;
// If your repo returns IQueryable, consider adding Include(r => r.Documents) inside it.

namespace ProjectAPI.Api.Application.Reservations.GetReservations
{
    public class GetReservationsHandler : IRequestHandler<GetReservationsQuery, PaginatedResponse<GetReservationsResponse>>
    {
        private readonly IReservationRepository _reservationRepository;
        private readonly ProjectScopeService _projectScope;
        private readonly ICurrentUser _currentUser;
        private readonly ApplicationDbContext _db;

        public GetReservationsHandler(
            IReservationRepository reservationRepository,
            ProjectScopeService projectScope,
            ICurrentUser currentUser,
            ApplicationDbContext db)
        {
            _reservationRepository = reservationRepository;
            _projectScope = projectScope;
            _currentUser = currentUser;
            _db = db;
        }

        public async Task<PaginatedResponse<GetReservationsResponse>> Handle(GetReservationsQuery request, CancellationToken cancellationToken)
        {
            // §6.4 — an internal caller only ever lists reservations inside
            // their assigned projects; null means unrestricted (GLOBAL_ADMIN).
            var scopedProjectIds = await _projectScope.GetScopedProjectIdsAsync(cancellationToken);

            // Project scope alone let any SALES_AGENT on a project see every
            // other agent's reservations on that same project — there was no
            // "this file is mine" boundary at all (unlike Appointments, which
            // enforces SalesAgentId == caller for agent callers). A SALES_AGENT
            // is now hard-scoped to their own AgentId; an explicit
            // request.AgentId for a different agent is ignored rather than
            // honoured, so an agent cannot widen their own query by asking for
            // someone else's id. Admins are unaffected.
            var effectiveAgentId = request.AgentId;
            if (_currentUser.IsInRole(RoleCodes.SalesAgent))
            {
                effectiveAgentId = _currentUser.UserId;
            }
            HashSet<Guid>? scopedUnitIds = null;
            if (scopedProjectIds is not null)
            {
                var ids = await _db.Set<UnitEntity>()
                    .Join(_db.Set<Immeuble>(), u => u.ProjectId, im => im.Id, (u, im) => new { u.Id, im.ProjectId })
                    .Where(x => scopedProjectIds.Contains(x.ProjectId))
                    .Select(x => x.Id)
                    .ToListAsync(cancellationToken);
                scopedUnitIds = ids.ToHashSet();
            }

            // Same Unit -> Immeuble join as the authorization perimeter above,
            // but driven by the caller's own filter choice rather than their
            // role — a project the caller can see, narrowed further.
            HashSet<Guid>? requestedUnitIds = null;
            if (request.ProjectId.HasValue || request.ImmeubleId.HasValue)
            {
                var ids = await _db.Set<UnitEntity>()
                    .Join(_db.Set<Immeuble>(), u => u.ProjectId, im => im.Id, (u, im) => new { u.Id, ImmeubleId = im.Id, im.ProjectId })
                    .Where(x => !request.ProjectId.HasValue || x.ProjectId == request.ProjectId.Value)
                    .Where(x => !request.ImmeubleId.HasValue || x.ImmeubleId == request.ImmeubleId.Value)
                    .Select(x => x.Id)
                    .ToListAsync(cancellationToken);
                requestedUnitIds = ids.ToHashSet();
            }

            var reservations = await _reservationRepository.Find(
                r =>
                    (string.IsNullOrEmpty(request.BuyerId) || r.BuyerId == request.BuyerId) &&
                    (string.IsNullOrEmpty(request.Name) || r.Name.Contains(request.Name)) &&
                    (string.IsNullOrEmpty(request.LastName) || r.LastName.Contains(request.LastName)) &&
                    (string.IsNullOrEmpty(request.CIN) || r.CIN == request.CIN) &&
                    (string.IsNullOrEmpty(request.Email) || r.Email.Contains(request.Email)) &&
                    (!request.UnitId.HasValue || r.UnitId == request.UnitId.Value) &&
                    (string.IsNullOrEmpty(effectiveAgentId) || r.AgentId == effectiveAgentId) &&
                    (string.IsNullOrEmpty(request.NotaireId) || r.NotaireId == request.NotaireId) &&
                    (!request.IsUnderConstruction.HasValue || r.IsUnderConstruction == request.IsUnderConstruction.Value) &&
                    (scopedUnitIds == null || scopedUnitIds.Contains(r.UnitId)) &&
                    (requestedUnitIds == null || requestedUnitIds.Contains(r.UnitId)) &&
                    (!request.Status.HasValue || r.Status == request.Status.Value),
                null
            );

            var totalItems = reservations.Count();

            var pageReservations = reservations
                // Stable order before paging: without it page contents are
                // nondeterministic and rows repeat or vanish between pages.
                .OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            // Reservation.UnitDetails is a label frozen at submit time, and it
            // is null on most historic rows — the list then fell back to
            // printing the raw UnitId, so the widest column on the admin table
            // read as "463d8967-5a10-4368-b30e-95cdd16baecc" instead of a unit
            // anyone could recognise. The unit itself is always still there, so
            // compose a readable label from it rather than showing a GUID.
            var pageUnitIds = pageReservations.Select(r => r.UnitId).Distinct().ToList();
            var unitInfo = await _db.Set<UnitEntity>()
                .Where(u => pageUnitIds.Contains(u.Id))
                .Join(_db.Set<Immeuble>(), u => u.ProjectId, im => im.Id,
                    (u, im) => new { u.Id, u.UnitNumber, u.TotalSurface, FloorName = u.Floor.Name, ImmeubleId = im.Id, ImmeubleName = im.Name, im.ProjectId })
                .Join(_db.Projects, x => x.ProjectId, p => p.Id,
                    (x, p) => new { x.Id, x.UnitNumber, x.TotalSurface, x.FloorName, x.ImmeubleId, x.ImmeubleName, ProjectId = p.Id, ProjectName = p.Name })
                .ToDictionaryAsync(x => x.Id, x => x, cancellationToken);

            string? UnitLabel(Guid unitId) =>
                unitInfo.TryGetValue(unitId, out var info)
                    ? string.Join(" · ", new[]
                        {
                            info.ImmeubleName,
                            info.UnitNumber,
                            info.TotalSurface > 0 ? $"{info.TotalSurface} m²" : null
                        }.Where(part => !string.IsNullOrWhiteSpace(part)))
                    : null;

            var paginatedData = pageReservations
                .Select(reservation =>
                {
                    unitInfo.TryGetValue(reservation.UnitId, out var info);
                    return new GetReservationsResponse
                    {
                        Id = reservation.Id,
                        BuyerId = reservation.BuyerId,
                        Name = reservation.Name,
                        LastName = reservation.LastName,
                        CIN = reservation.CIN,
                        Email = reservation.Email,
                        PhoneNumber = reservation.PhoneNumber,
                        UnitId = reservation.UnitId,
                        // Frozen label when it exists, otherwise composed live from
                        // the unit so the column never degrades to a GUID.
                        UnitDetails = !string.IsNullOrWhiteSpace(reservation.UnitDetails)
                            ? reservation.UnitDetails
                            : UnitLabel(reservation.UnitId),
                        ProjectId = info?.ProjectId,
                        ProjectName = info?.ProjectName,
                        ImmeubleId = info?.ImmeubleId,
                        ImmeubleName = info?.ImmeubleName,
                        FloorName = info?.FloorName,
                        UnitNumber = info?.UnitNumber,
                        AgentId = reservation.AgentId,
                        NotaireId = reservation.NotaireId,
                        TotalPropertyPrice = reservation.TotalPropertyPrice,
                        ReservationAmount = reservation.ReservationAmount,
                        ReservationDate = reservation.ReservationDate,
                        IsUnderConstruction = reservation.IsUnderConstruction,

                        // NEW fields
                        Status = reservation.Status,
                        CreatedAt = reservation.CreatedAt,
                        ValidatedAt = reservation.ValidatedAt,
                        ValidatedBy = reservation.ValidatedBy,
                        AdminNote = reservation.AdminNote,

                        // Documents projection — adjust properties to match your entity
                        Documents = reservation.Documents.Select(d => new ReservationDocumentResponse
                        {
                            Id = d.Id,
                            Name = d.FileName,           // adjust if different
                            Url = d.Url,             // adjust if different
                            UploadedAt = d.UploadedAt, // adjust if different
                            UploadedBy = d.UploadedBy  // optional
                        }).ToList()
                    };
                })
                .ToList();

            var contexts = await UnitLocations.ForUnitsAsync(_db, paginatedData.Select(r => r.UnitId), cancellationToken);
            foreach (var row in paginatedData) row.UnitContext = contexts.GetValueOrDefault(row.UnitId);

            return new PaginatedResponse<GetReservationsResponse>(paginatedData, request.PageNumber, request.PageSize, totalItems);
        }
    }
}
