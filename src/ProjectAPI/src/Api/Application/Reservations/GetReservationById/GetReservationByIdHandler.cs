using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Domain.Reservations.Interface;

namespace ProjectAPI.Api.Application.Reservations.GetReservationById;

public class GetReservationByIdHandler : IRequestHandler<GetReservationByIdQuery, GetReservationByIdResponse>
{
    private readonly IReservationRepository _reservationRepository;

    public GetReservationByIdHandler(IReservationRepository reservationRepository)
    {
        _reservationRepository = reservationRepository;
    }

    public async Task<GetReservationByIdResponse> Handle(GetReservationByIdQuery request, CancellationToken cancellationToken)
    {
        if (!request.ReservationId.HasValue)
            throw new NotFoundException("ReservationId is required.");
            
        // Use GetByIdWithDocumentsAsync to include documents in the response
        var reservation = await _reservationRepository.GetByIdWithDocumentsAsync(request.ReservationId.Value)
            ?? throw new NotFoundException($"Reservation with ID {request.ReservationId} not found.");

        return new GetReservationByIdResponse
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
            TotalPropertyPrice = reservation.TotalPropertyPrice,
            ReservationAmount = reservation.ReservationAmount,
            ReservationDate = reservation.ReservationDate,
            IsUnderConstruction = reservation.IsUnderConstruction,

            // NEW
            Status = reservation.Status,
            CreatedAt = reservation.CreatedAt,
            ValidatedAt = reservation.ValidatedAt,
            ValidatedBy = reservation.ValidatedBy,
            AdminNote = reservation.AdminNote,
            Documents = reservation.Documents.Select(d => new ReservationDocumentResponse
            {
                Id = d.Id,
                Name = d.FileName,             // adjust to your entity
                Url = d.Url,               // adjust to your entity
                UploadedAt = d.UploadedAt, // adjust to your entity
                UploadedBy = d.UploadedBy  // optional
            }).ToList()
        };
    }
}
