using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Files;
using ProjectAPI.Api.Application.Common.Notifications;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Reservations.UploadReservationDocument;
using ProjectAPI.Domain.Common.Interfaces;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Reservations.UploadReservationContract;

/// <summary>
/// The contract step of an approved reservation, filed by the sales
/// administrator (project admin as fallback):
/// <code>
///   None ──contract──▶ Issued ──signed scan──▶ Signed
///          Issued ──contract──▶ Issued   (replaced before signature; old version kept)
/// </code>
/// The reservation itself stays Approved throughout.
/// </summary>
public class UploadReservationContractCommand : IRequest<UploadReservationContractResponse>
{
    public Guid ReservationId { get; set; }
    public IFormFile? File { get; set; }

    /// <summary>False: the contract to sign. True: the scan of the signed paper copy.</summary>
    public bool Signed { get; set; }
}

public class UploadReservationContractResponse
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public ReservationContractStatus ContractStatus { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class UploadReservationContractHandler
    : IRequestHandler<UploadReservationContractCommand, UploadReservationContractResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly IBlobStorageService _blobStorage;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;
    private readonly INotificationService _notifications;

    public UploadReservationContractHandler(
        ApplicationDbContext db,
        IBlobStorageService blobStorage,
        ProjectScopeService projectScope,
        ICurrentUser currentUser,
        INotificationService notifications)
    {
        _db = db;
        _blobStorage = blobStorage;
        _projectScope = projectScope;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    public async Task<UploadReservationContractResponse> Handle(UploadReservationContractCommand request, CancellationToken ct)
    {
        await _projectScope.EnsureReservationAccessAsync(request.ReservationId, ct);

        var reservation = await _db.Set<Reservation>()
            .FirstOrDefaultAsync(r => r.Id == request.ReservationId, ct)
            ?? throw new NotFoundException($"Reservation {request.ReservationId} not found.");

        if (reservation.Status != ReservationStatus.Approved)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.InvalidStatusTransition,
                $"Le contrat ne peut être déposé que sur une réservation approuvée (statut actuel : {reservation.Status}).",
                StatusCodes.Status409Conflict);
        }

        if (request.Signed && reservation.ContractStatus != ReservationContractStatus.Issued)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.InvalidStatusTransition,
                reservation.ContractStatus == ReservationContractStatus.Signed
                    ? "Le contrat signé est déjà classé sur ce dossier."
                    : "Déposez d'abord le contrat à signer avant sa version signée.",
                StatusCodes.Status409Conflict);
        }

        if (!request.Signed && reservation.ContractStatus == ReservationContractStatus.Signed)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.InvalidStatusTransition,
                "Le contrat signé est déjà classé : le contrat ne peut plus être remplacé.",
                StatusCodes.Status409Conflict);
        }

        var file = await UploadedFileInspector.InspectAsync(request.File, nameof(request.File), ct);

        var blobName = $"{reservation.Id}/{Guid.NewGuid()}_{file.FileName}";
        string url;
        await using (var stream = request.File!.OpenReadStream())
        {
            url = await _blobStorage.ForContainer(UploadReservationDocumentHandler.ContainerName)
                .UploadAsync(blobName, stream, file.ContentType, ct);
        }

        var now = DateTime.UtcNow;
        var document = new ReservationDocument
        {
            Id = Guid.NewGuid(),
            ReservationId = reservation.Id,
            FileName = file.FileName,
            Url = url,
            ContentType = file.ContentType,
            SizeBytes = file.SizeBytes,
            DocumentType = request.Signed
                ? ReservationContractDocumentTypes.SignedContract
                : ReservationContractDocumentTypes.Contract,
            UploadedAt = now,
            UploadedBy = _currentUser.UserId,
        };
        _db.Add(document);

        if (request.Signed)
        {
            reservation.ContractStatus = ReservationContractStatus.Signed;
            reservation.ContractSignedAt = now;
            reservation.ContractSignedBy = _currentUser.UserId;
        }
        else
        {
            reservation.ContractStatus = ReservationContractStatus.Issued;
            reservation.ContractIssuedAt = now;
            reservation.ContractIssuedBy = _currentUser.UserId;
        }

        // RowVersion guards two administrators filing at once (409 RESOURCE_VERSION_CONFLICT).
        await _db.SaveChangesAsync(ct);

        // Non-fatal, like every notification: the filing is already durable.
        var (title, body) = request.Signed
            ? ("Contrat signé enregistré", "Votre contrat signé a été enregistré sur votre dossier.")
            : ("Contrat prêt à signer", "Votre contrat de réservation est prêt. Rapprochez-vous de votre conseiller pour le signer.");
        if (!string.IsNullOrWhiteSpace(reservation.BuyerId))
        {
            await _notifications.NotifyAsync(
                reservation.BuyerId, request.Signed ? "CONTRACT_SIGNED" : "CONTRACT_ISSUED",
                title, body, reservation.Id, "Reservation", ct);
        }
        if (!string.IsNullOrWhiteSpace(reservation.AgentId))
        {
            await _notifications.NotifyAsync(
                reservation.AgentId, request.Signed ? "CONTRACT_SIGNED" : "CONTRACT_ISSUED",
                title, $"Réservation {reservation.UnitDetails} : {title.ToLowerInvariant()}.",
                reservation.Id, "Reservation", ct);
        }

        return new UploadReservationContractResponse
        {
            DocumentId = document.Id,
            FileName = document.FileName,
            ContractStatus = reservation.ContractStatus,
            UploadedAt = document.UploadedAt,
        };
    }
}
