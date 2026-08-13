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
                    (scopedUnitIds == null || scopedUnitIds.Contains(r.UnitId)),
                null
            );

            var totalItems = reservations.Count();

            var paginatedData = reservations
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(reservation => new GetReservationsResponse
                {
                    Id = reservation.Id,
                    BuyerId = reservation.BuyerId,
                    Name = reservation.Name,
                    LastName = reservation.LastName,
                    CIN = reservation.CIN,
                    Email = reservation.Email,
                    PhoneNumber = reservation.PhoneNumber,
                    UnitId = reservation.UnitId,
                    UnitDetails = reservation.UnitDetails,
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
                })
                .ToList();

            return new PaginatedResponse<GetReservationsResponse>(paginatedData, request.PageNumber, request.PageSize, totalItems);
        }
    }
}
