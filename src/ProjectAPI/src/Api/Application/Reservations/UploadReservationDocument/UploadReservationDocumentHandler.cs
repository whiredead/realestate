using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Common.Interfaces;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Reservations.UploadReservationDocument;

public class UploadReservationDocumentHandler : IRequestHandler<UploadReservationDocumentCommand, UploadReservationDocumentResponse>
{
    /// <summary>
    /// Reservation documents (contracts, CIN scans, blueprints) live in their
    /// own container, separate from the public "images" container used by
    /// property photos — same connection (BlobStorageSettings), different
    /// container, chosen per call via IBlobStorageService.ForContainer.
    /// </summary>
    public const string ContainerName = "documents";

    private readonly ApplicationDbContext _db;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;

    public UploadReservationDocumentHandler(
        ApplicationDbContext db,
        IBlobStorageService blobStorageService,
        ProjectScopeService projectScope,
        ICurrentUser currentUser)
    {
        _db = db;
        _blobStorageService = blobStorageService;
        _projectScope = projectScope;
        _currentUser = currentUser;
    }

    public async Task<UploadReservationDocumentResponse> Handle(UploadReservationDocumentCommand request, CancellationToken cancellationToken)
    {
        await _projectScope.EnsureReservationAccessAsync(request.ReservationId, cancellationToken);

        var reservationExists = await _db.Set<Reservation>().AnyAsync(r => r.Id == request.ReservationId, cancellationToken);
        if (!reservationExists)
            throw new KeyNotFoundException($"Reservation {request.ReservationId} not found.");

        var blobName = $"{request.ReservationId}/{Guid.NewGuid()}_{Path.GetFileName(request.File.FileName)}";
        var contentType = request.File.ContentType ?? "application/octet-stream";

        using var stream = request.File.OpenReadStream();
        var url = await _blobStorageService.ForContainer(ContainerName)
            .UploadAsync(blobName, stream, contentType, cancellationToken);

        var document = new ReservationDocument
        {
            Id = Guid.NewGuid(),
            ReservationId = request.ReservationId,
            FileName = Path.GetFileName(request.File.FileName),
            Url = url,
            ContentType = contentType,
            SizeBytes = request.File.Length,
            DocumentType = request.DocumentType,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = _currentUser.UserId,
        };

        _db.Set<ReservationDocument>().Add(document);
        await _db.SaveChangesAsync(cancellationToken);

        return new UploadReservationDocumentResponse
        {
            Id = document.Id,
            FileName = document.FileName,
            Url = document.Url,
            UploadedAt = document.UploadedAt,
        };
    }
}
