using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Models;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Reservations.Interface;
using ProjectAPI.Domain.Users.Entities;

namespace ProjectAPI.Api.Application.Reservations.GetReservationById;

public class GetReservationByIdHandler : IRequestHandler<GetReservationByIdQuery, GetReservationByIdResponse>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;

    public GetReservationByIdHandler(IReservationRepository reservationRepository, ProjectScopeService projectScope, ICurrentUser currentUser)
    {
        _reservationRepository = reservationRepository;
        _projectScope = projectScope;
        _currentUser = currentUser;
    }

    public async Task<GetReservationByIdResponse> Handle(GetReservationByIdQuery request, CancellationToken cancellationToken)
    {
        if (!request.ReservationId.HasValue)
            throw new NotFoundException("ReservationId is required.");

        // §6.4 — internal roles are scoped to their assigned projects; a buyer
        // may only read their own reservation (never another buyer's file).
        await _projectScope.EnsureReservationAccessAsync(request.ReservationId.Value, cancellationToken);
        await _projectScope.EnsureBuyerOwnsReservationAsync(request.ReservationId.Value, cancellationToken);

        // Use GetByIdWithDocumentsAsync to include documents in the response
        var reservation = await _reservationRepository.GetByIdWithDocumentsAsync(request.ReservationId.Value)
            ?? throw new NotFoundException($"Reservation with ID {request.ReservationId} not found.");

        // Project scope alone let any SALES_AGENT on the project open a
        // colleague's reservation file — there was no "is this mine" check
        // at all, unlike Appointments. Same non-disclosure posture as
        // ProjectScopeDenied: refuse rather than reveal whether the file
        // exists to an agent it doesn't belong to.
        if (_currentUser.IsInRole(RoleCodes.SalesAgent) && reservation.AgentId != _currentUser.UserId)
        {
            throw new Common.Exceptions.BusinessRuleException(
                Common.Exceptions.BusinessErrorCodes.Unauthorized,
                "Ce dossier n'est pas accessible.",
                StatusCodes.Status403Forbidden);
        }

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
            }).ToList(),

            CatalogPrice = reservation.CatalogPrice,
            Discount = reservation.Discount,
            FinalPrice = reservation.FinalPrice,
            NotaireId = reservation.NotaireId,
            ExpiresAt = reservation.ExpiresAt,
            CoBuyers = reservation.Buyers.Select(b => new ReservationCoBuyerResponse
            {
                CrmContactId = b.CrmContactId,
                FirstName = b.CrmContact.FirstName,
                LastName = b.CrmContact.LastName,
                Email = b.CrmContact.Email,
                Phone = b.CrmContact.Phone,
                OwnershipPercent = b.OwnershipPercent
            }).ToList()
        };
    }
}
