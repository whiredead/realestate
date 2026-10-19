using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Files;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Common.Interfaces;
using ProjectAPI.Domain.Payments.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Payments.PaymentProofs;

public static class PaymentProofStorage
{
    /// <summary>Private container, separate from reservation dossiers ("documents") and catalogue images.</summary>
    public const string ContainerName = "payment-proofs";
}

/// <summary>
/// Step 1 of recording a payment: upload the evidence (photo or document).
/// The returned id is then sent as <c>ProofId</c> with the payment itself.
/// </summary>
public class UploadPaymentProofCommand : IRequest<PaymentProofResponse>
{
    public Guid ReservationId { get; set; }
    public IFormFile? File { get; set; }
}

public class PaymentProofResponse
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class UploadPaymentProofHandler : IRequestHandler<UploadPaymentProofCommand, PaymentProofResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly IBlobStorageService _blobStorage;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;

    public UploadPaymentProofHandler(
        ApplicationDbContext db, IBlobStorageService blobStorage, ProjectScopeService projectScope, ICurrentUser currentUser)
    {
        _db = db;
        _blobStorage = blobStorage;
        _projectScope = projectScope;
        _currentUser = currentUser;
    }

    public async Task<PaymentProofResponse> Handle(UploadPaymentProofCommand request, CancellationToken ct)
    {
        await _projectScope.EnsureReservationAccessAsync(request.ReservationId, ct);

        if (!await _db.Set<Reservation>().AnyAsync(r => r.Id == request.ReservationId, ct))
        {
            throw new NotFoundException($"Reservation {request.ReservationId} not found.");
        }

        var file = await UploadedFileInspector.InspectAsync(request.File, nameof(request.File), ct);

        var blobName = $"{request.ReservationId}/{Guid.NewGuid()}_{file.FileName}";
        await using (var stream = request.File!.OpenReadStream())
        {
            await _blobStorage.ForContainer(PaymentProofStorage.ContainerName)
                .UploadAsync(blobName, stream, file.ContentType, ct);
        }

        var proof = new PaymentProof
        {
            Id = Guid.NewGuid(),
            ReservationId = request.ReservationId,
            BlobName = blobName,
            FileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.SizeBytes,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = _currentUser.UserId,
        };
        _db.Add(proof);
        await _db.SaveChangesAsync(ct);

        return new PaymentProofResponse
        {
            Id = proof.Id,
            ReservationId = proof.ReservationId,
            FileName = proof.FileName,
            ContentType = proof.ContentType,
            SizeBytes = proof.SizeBytes,
            UploadedAt = proof.UploadedAt,
        };
    }
}

/// <summary>Streams a proof to a caller entitled to the reservation's ledger (the buyer reads their own).</summary>
public class DownloadPaymentProofQuery : IRequest<PaymentProofContent>
{
    public Guid ProofId { get; set; }
}

public class PaymentProofContent
{
    public Stream Content { get; set; } = Stream.Null;
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileName { get; set; } = "justificatif";
}

public class DownloadPaymentProofHandler : IRequestHandler<DownloadPaymentProofQuery, PaymentProofContent>
{
    /// <summary>Internal roles that work the ledger. Technicians and notaries have no business reading receipts.</summary>
    private static readonly string[] LedgerReaderRoles =
    {
        RoleCodes.GlobalAdmin, RoleCodes.ProjectAdmin, RoleCodes.SalesAdmin, RoleCodes.SalesAgent
    };

    private readonly ApplicationDbContext _db;
    private readonly IBlobStorageService _blobStorage;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;

    public DownloadPaymentProofHandler(
        ApplicationDbContext db, IBlobStorageService blobStorage, ProjectScopeService projectScope, ICurrentUser currentUser)
    {
        _db = db;
        _blobStorage = blobStorage;
        _projectScope = projectScope;
        _currentUser = currentUser;
    }

    public async Task<PaymentProofContent> Handle(DownloadPaymentProofQuery request, CancellationToken ct)
    {
        var proof = await _db.Set<PaymentProof>().AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProofId, ct)
            ?? throw new NotFoundException($"Payment proof {request.ProofId} not found.");

        var isInternal = _currentUser.Roles.Any(r => RoleCodes.Internal.Contains(r, StringComparer.Ordinal));
        if (isInternal && !_currentUser.Roles.Any(r => LedgerReaderRoles.Contains(r, StringComparer.Ordinal)))
        {
            throw BusinessRuleException.BuyerScopeDenied();
        }

        await _projectScope.EnsureReservationAccessAsync(proof.ReservationId, ct);
        await _projectScope.EnsureBuyerOwnsReservationAsync(proof.ReservationId, ct);

        var (content, contentType, exists) = await _blobStorage
            .ForContainer(PaymentProofStorage.ContainerName)
            .DownloadAsync(proof.BlobName, ct);

        if (!exists || content is null)
        {
            throw new NotFoundException($"The file for payment proof {request.ProofId} is no longer in storage.");
        }

        return new PaymentProofContent
        {
            Content = content,
            ContentType = proof.ContentType ?? contentType ?? "application/octet-stream",
            FileName = proof.FileName,
        };
    }
}
