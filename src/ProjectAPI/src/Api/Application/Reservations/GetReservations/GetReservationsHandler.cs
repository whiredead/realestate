using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Domain.Reservations.Interface;
// If your repo returns IQueryable, consider adding Include(r => r.Documents) inside it.

namespace ProjectAPI.Api.Application.Reservations.GetReservations
{
    public class GetReservationsHandler : IRequestHandler<GetReservationsQuery, PaginatedResponse<GetReservationsResponse>>
    {
        private readonly IReservationRepository _reservationRepository;

        public GetReservationsHandler(IReservationRepository reservationRepository)
        {
            _reservationRepository = reservationRepository;
        }

        public async Task<PaginatedResponse<GetReservationsResponse>> Handle(GetReservationsQuery request, CancellationToken cancellationToken)
        {
            var reservations = await _reservationRepository.Find(
                r =>
                    (string.IsNullOrEmpty(request.BuyerId) || r.BuyerId == request.BuyerId) &&
                    (string.IsNullOrEmpty(request.Name) || r.Name.Contains(request.Name)) &&
                    (string.IsNullOrEmpty(request.LastName) || r.LastName.Contains(request.LastName)) &&
                    (string.IsNullOrEmpty(request.CIN) || r.CIN == request.CIN) &&
                    (string.IsNullOrEmpty(request.Email) || r.Email.Contains(request.Email)) &&
                    (!request.UnitId.HasValue || r.UnitId == request.UnitId.Value) &&
                    (string.IsNullOrEmpty(request.AgentId) || r.AgentId == request.AgentId) &&
                    (string.IsNullOrEmpty(request.NotaireId) || r.NotaireId == request.NotaireId) &&
                    (!request.IsUnderConstruction.HasValue || r.IsUnderConstruction == request.IsUnderConstruction.Value),
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
