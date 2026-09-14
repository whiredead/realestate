using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Reservations.UploadReservationDocument;
using ProjectAPI.Domain.Common.Interfaces;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Reservations.DownloadReservationDocument;

/// <summary>
/// §24.2/§34 — streams a reservation document to a caller who is allowed to see
/// that reservation.
///
/// The stored <c>ReservationDocument.Url</c> is the raw blob URI, and the
/// console linked straight to it. That only ever works one of two ways, and
/// both are wrong: either the container is private and the link 403s for
/// everybody, or it is public and every CIN scan and signed contract is
/// readable by anyone holding the URL — no login, no perimeter, no audit. The
/// container is now created private (see BlobStorageService), and this is how a
/// document is read instead: through the same project-perimeter and
/// buyer-ownership checks as the reservation it belongs to.
/// </summary>
public class DownloadReservationDocumentQuery : IRequest<ReservationDocumentContent>
{
    public Guid DocumentId { get; set; }
}

public class ReservationDocumentContent
{
    public Stream Content { get; set; } = Stream.Null;
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileName { get; set; } = "document";
}

public class DownloadReservationDocumentHandler
    : IRequestHandler<DownloadReservationDocumentQuery, ReservationDocumentContent>
{
    private readonly ApplicationDbContext _db;
    private readonly IBlobStorageService _blobStorage;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// Internal roles that work a reservation file. Technicians and the
    /// technical lead are staffed on the same projects for after-sales work but
    /// have no business reading a buyer's identity papers or signed contract.
    /// </summary>
    private static readonly string[] FileReaderRoles =
    {
        RoleCodes.GlobalAdmin, RoleCodes.ProjectAdmin, RoleCodes.SalesAgent, RoleCodes.Notary
    };

    public DownloadReservationDocumentHandler(
        ApplicationDbContext db,
        IBlobStorageService blobStorage,
        ProjectScopeService projectScope,
        ICurrentUser currentUser)
    {
        _db = db;
        _blobStorage = blobStorage;
        _projectScope = projectScope;
        _currentUser = currentUser;
    }

    public async Task<ReservationDocumentContent> Handle(
        DownloadReservationDocumentQuery request, CancellationToken ct)
    {
        var document = await _db.Set<ReservationDocument>()
            .FirstOrDefaultAsync(d => d.Id == request.DocumentId, ct)
            ?? throw new NotFoundException($"Document {request.DocumentId} not found.");

        // Both shapes of caller: an internal role is checked by project
        // perimeter, a buyer by ownership of the file. Either alone would let
        // the other through.
        var isInternal = _currentUser.Roles.Any(r => RoleCodes.Internal.Contains(r, StringComparer.Ordinal));
        if (isInternal && !_currentUser.Roles.Any(r => FileReaderRoles.Contains(r, StringComparer.Ordinal)))
        {
            // Technician / tech lead: in the project perimeter, but not a reader of reservation files.
            throw BusinessRuleException.BuyerScopeDenied();
        }

        await _projectScope.EnsureReservationAccessAsync(document.ReservationId, ct);
        await _projectScope.EnsureBuyerOwnsReservationAsync(document.ReservationId, ct);

        var blobName = ToBlobName(document.Url);

        var (content, contentType, exists) = await _blobStorage
            .ForContainer(UploadReservationDocumentHandler.ContainerName)
            .DownloadAsync(blobName, ct);

        if (!exists || content is null)
        {
            throw new NotFoundException($"The file for document {request.DocumentId} is no longer in storage.");
        }

        return new ReservationDocumentContent
        {
            Content = content,
            ContentType = document.ContentType ?? contentType ?? "application/octet-stream",
            FileName = document.FileName
        };
    }

    /// <summary>
    /// The stored Url is the blob's full URI (…/documents/{blobName}); strip the
    /// origin and container prefix back off. Same transformation
    /// DeleteReservationDocumentHandler does — kept identical on purpose, since
    /// the two must agree on which blob a row points at.
    /// </summary>
    private static string ToBlobName(string url)
    {
        var path = new Uri(url).AbsolutePath.TrimStart('/');
        var prefix = $"{UploadReservationDocumentHandler.ContainerName}/";

        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? path[prefix.Length..]
            : path;
    }
}
