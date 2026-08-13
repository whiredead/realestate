using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Reservations.UploadReservationDocument;
using ProjectAPI.Domain.Common.Interfaces;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Reservations.DeleteReservationDocument;

public class DeleteReservationDocumentHandler : IRequestHandler<DeleteReservationDocumentCommand, DeleteReservationDocumentResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ProjectScopeService _projectScope;

    public DeleteReservationDocumentHandler(ApplicationDbContext db, IBlobStorageService blobStorageService, ProjectScopeService projectScope)
    {
        _db = db;
        _blobStorageService = blobStorageService;
        _projectScope = projectScope;
    }

    public async Task<DeleteReservationDocumentResponse> Handle(DeleteReservationDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await _db.Set<ReservationDocument>().FirstOrDefaultAsync(d => d.Id == request.DocumentId, cancellationToken);
        if (document == null)
            return new DeleteReservationDocumentResponse { IsSuccess = false, Message = "Document not found." };

        await _projectScope.EnsureReservationAccessAsync(document.ReservationId, cancellationToken);

        // The stored Url is the blob's full URI (…/documents/{blobName}); the
        // container client only needs the path segment after the container.
        var uri = new Uri(document.Url);
        var blobName = uri.AbsolutePath.TrimStart('/');
        var containerPrefix = $"{UploadReservationDocumentHandler.ContainerName}/";
        if (blobName.StartsWith(containerPrefix, StringComparison.OrdinalIgnoreCase))
            blobName = blobName[containerPrefix.Length..];

        await _blobStorageService.ForContainer(UploadReservationDocumentHandler.ContainerName)
            .DeleteAsync(blobName, cancellationToken);

        _db.Set<ReservationDocument>().Remove(document);
        await _db.SaveChangesAsync(cancellationToken);

        return new DeleteReservationDocumentResponse { IsSuccess = true, Message = "Document deleted." };
    }
}
