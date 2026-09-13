using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Domain.Common.Interfaces;
using ProjectAPI.Domain.Sales.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Sales.AfterSales.Attachments;

/// <summary>
/// §20/§24.2 — evidence photographs on a SAV claim.
///
/// Replaces the previous arrangement, where the create-claim command took a
/// list of client-supplied URLs and stored them verbatim: the "attachment" was
/// whatever string the caller sent, pointing anywhere at all. Files now arrive
/// as files, land in a PRIVATE container, and are read back only through
/// <see cref="DownloadClaimAttachmentQuery"/>, which re-checks who is asking.
/// </summary>
public static class ClaimAttachmentStorage
{
    /// <summary>
    /// Separate from "documents" (reservation dossiers) and "images" (the
    /// public catalogue). A claim photo is neither: it is private evidence
    /// attached to one buyer's file.
    /// </summary>
    public const string ContainerName = "claim-attachments";

    /// <summary>
    /// What a buyer can plausibly photograph a defect with, plus PDF for a
    /// written report. Anything else — scripts, archives, office documents with
    /// macros — has no business in this container and is refused by type rather
    /// than by trusting the extension.
    /// </summary>
    public static readonly string[] AllowedContentTypes =
    {
        "image/jpeg", "image/png", "image/webp", "image/heic", "image/heif",
        "video/mp4", "video/quicktime",
        "application/pdf"
    };

    /// <summary>25 MB — a phone photo or a short clip, not a video library.</summary>
    public const long MaxSizeBytes = 25 * 1024 * 1024;
}

public class UploadClaimAttachmentCommand : IRequest<ClaimAttachmentResponse>
{
    public Guid ClaimId { get; set; }
    public IFormFile File { get; set; } = null!;

    /// <summary>Optional BEFORE / AFTER (technician's intervention photos).</summary>
    public string? Phase { get; set; }
}

public class ClaimAttachmentResponse
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class UploadClaimAttachmentHandler : IRequestHandler<UploadClaimAttachmentCommand, ClaimAttachmentResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly IBlobStorageService _blobStorage;
    private readonly ICurrentUser _currentUser;
    private readonly ProjectScopeService _projectScope;

    public UploadClaimAttachmentHandler(
        ApplicationDbContext db,
        IBlobStorageService blobStorage,
        ICurrentUser currentUser,
        ProjectScopeService projectScope)
    {
        _projectScope = projectScope;
        _db = db;
        _blobStorage = blobStorage;
        _currentUser = currentUser;
    }

    public async Task<ClaimAttachmentResponse> Handle(UploadClaimAttachmentCommand request, CancellationToken ct)
    {
        var claim = await _db.Set<AfterSaleClaim>()
            .FirstOrDefaultAsync(c => c.Id == request.ClaimId, ct)
            ?? throw new NotFoundException($"Claim {request.ClaimId} not found.");

        await ClaimDetail.ClaimAccessPolicy.EnsureCanAccessAsync(_db, _projectScope, _currentUser, claim, ct);

        string? phase = null;
        if (!string.IsNullOrWhiteSpace(request.Phase))
        {
            phase = request.Phase.Trim().ToUpperInvariant();
            if (!ClaimDetail.ClaimAttachmentPhases.All.Contains(phase))
            {
                throw new Common.Exceptions.ValidationException(new[] { new FluentValidation.Results.ValidationFailure("Phase", "La phase doit être BEFORE ou AFTER.") });
            }
            if (!ClaimDetail.ClaimAccessPolicy.IsSupervisor(_currentUser) && !ClaimDetail.ClaimAccessPolicy.IsAssignedTechnician(_currentUser, claim))
            {
                throw BusinessRuleException.BuyerScopeDenied();
            }
        }

        var contentType = request.File.ContentType ?? "application/octet-stream";

        if (!ClaimAttachmentStorage.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                $"Type de fichier non autorisé ({contentType}). " +
                "Formats acceptés : photo (JPEG, PNG, WebP, HEIC), vidéo (MP4, MOV) ou PDF.");
        }

        if (request.File.Length > ClaimAttachmentStorage.MaxSizeBytes)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                $"Fichier trop volumineux ({request.File.Length / 1024 / 1024} Mo). Maximum : 25 Mo.");
        }

        // Guid prefix so re-uploading "photo.jpg" never overwrites the first,
        // and the claim id groups a file with the claim it evidences.
        var blobName = $"{claim.Id}/{Guid.NewGuid()}_{Path.GetFileName(request.File.FileName)}";

        using var stream = request.File.OpenReadStream();
        var url = await _blobStorage.ForContainer(ClaimAttachmentStorage.ContainerName)
            .UploadAsync(blobName, stream, contentType, ct);

        var attachment = new ClaimAttachment
        {
            Id = Guid.NewGuid(),
            ClaimId = claim.Id,
            Url = url,
            FileName = Path.GetFileName(request.File.FileName),
            ContentType = contentType,
            Phase = phase,
            SizeBytes = request.File.Length,
            // From the token, never the body.
            UploadedByUserId = _currentUser.UserId,
            UploadedAt = DateTime.UtcNow
        };

        _db.Add(attachment);
        await _db.SaveChangesAsync(ct);

        return new ClaimAttachmentResponse
        {
            Id = attachment.Id,
            ClaimId = attachment.ClaimId,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            SizeBytes = attachment.SizeBytes,
            UploadedAt = attachment.UploadedAt
        };
    }
}

/// <summary>
/// Streams one attachment to a caller entitled to see it. The stored Url is a
/// private blob address and is deliberately never returned to the client.
/// </summary>
public class DownloadClaimAttachmentQuery : IRequest<ClaimAttachmentContent>
{
    public Guid AttachmentId { get; set; }
}

public class ClaimAttachmentContent
{
    public Stream Content { get; set; } = Stream.Null;
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileName { get; set; } = "attachment";
}

public class DownloadClaimAttachmentHandler : IRequestHandler<DownloadClaimAttachmentQuery, ClaimAttachmentContent>
{
    private readonly ApplicationDbContext _db;
    private readonly IBlobStorageService _blobStorage;
    private readonly ICurrentUser _currentUser;

    private readonly ProjectScopeService _projectScope;

    public DownloadClaimAttachmentHandler(
        ApplicationDbContext db,
        IBlobStorageService blobStorage,
        ICurrentUser currentUser,
        ProjectScopeService projectScope)
    {
        _projectScope = projectScope;
        _db = db;
        _blobStorage = blobStorage;
        _currentUser = currentUser;
    }

    public async Task<ClaimAttachmentContent> Handle(DownloadClaimAttachmentQuery request, CancellationToken ct)
    {
        var attachment = await _db.Set<ClaimAttachment>()
            .FirstOrDefaultAsync(a => a.Id == request.AttachmentId, ct)
            ?? throw new NotFoundException($"Attachment {request.AttachmentId} not found.");

        var claim = await _db.Set<AfterSaleClaim>()
            .FirstOrDefaultAsync(c => c.Id == attachment.ClaimId, ct)
            ?? throw new NotFoundException($"Claim {attachment.ClaimId} not found.");

        await ClaimDetail.ClaimAccessPolicy.EnsureCanAccessAsync(_db, _projectScope, _currentUser, claim, ct);

        var blobName = ToBlobName(attachment.Url);

        var (content, contentType, exists) = await _blobStorage
            .ForContainer(ClaimAttachmentStorage.ContainerName)
            .DownloadAsync(blobName, ct);

        if (!exists || content is null)
        {
            throw new NotFoundException($"The file for attachment {request.AttachmentId} is no longer in storage.");
        }

        return new ClaimAttachmentContent
        {
            Content = content,
            ContentType = attachment.ContentType ?? contentType ?? "application/octet-stream",
            FileName = attachment.FileName ?? "attachment"
        };
    }

    private static string ToBlobName(string url)
    {
        var path = new Uri(url).AbsolutePath.TrimStart('/');
        var prefix = $"{ClaimAttachmentStorage.ContainerName}/";

        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? path[prefix.Length..]
            : path;
    }
}

/// <summary>
/// Who may see a claim, and therefore its evidence (§6.3/§6.4).
///
/// One place rather than two: upload and download must agree, and a check that
/// exists twice is a check that will eventually disagree with itself.
/// </summary>
internal static class ClaimAccess
{
    public static Task EnsureCanAccessAsync(
        ApplicationDbContext db, ICurrentUser user, AfterSaleClaim claim, CancellationToken ct)
    {
        // Admins and technicians handle claims across the SAV desk — that is
        // the whole point of RoleGroups.AdminsTechnicians on this controller.
        var isInternal = user.Roles.Any(r => RoleCodes.Internal.Contains(r, StringComparer.Ordinal));
        if (isInternal) return Task.CompletedTask;

        // A buyer sees their own claim and nothing else.
        if (!string.IsNullOrEmpty(claim.BuyerId) &&
            string.Equals(claim.BuyerId, user.UserId, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        throw BusinessRuleException.BuyerScopeDenied();
    }
}
