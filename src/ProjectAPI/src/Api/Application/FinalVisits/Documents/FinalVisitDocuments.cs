using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Files;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Common.Interfaces;
using ProjectAPI.Domain.FinalVisits.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Users.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.FinalVisits.Documents;

public static class FinalVisitDocumentStorage
{
    /// <summary>Private container, separate from reservation dossiers and claim photos.</summary>
    public const string ContainerName = "final-visit-documents";

    /// <summary>Every document of a case, newest first, for the case and buyer report views.</summary>
    public static async Task<List<FinalVisitDocumentDto>> ListAsync(ApplicationDbContext db, Guid caseId, CancellationToken ct) =>
        await db.Set<FinalVisitDocument>().AsNoTracking()
            .Where(d => d.CaseId == caseId)
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new FinalVisitDocumentDto
            {
                Id = d.Id,
                Kind = d.Kind,
                ReportId = d.ReportId,
                SnagId = d.SnagId,
                FileName = d.FileName,
                ContentType = d.ContentType,
                SizeBytes = d.SizeBytes,
                UploadedAt = d.UploadedAt,
            })
            .ToListAsync(ct);

    public static Task<bool> HasSignedPvAsync(ApplicationDbContext db, Guid reservationId, CancellationToken ct) =>
        (from d in db.Set<FinalVisitDocument>()
         join c in db.Set<FinalVisitCase>() on d.CaseId equals c.Id
         where c.ReservationId == reservationId && d.Kind == FinalVisitDocumentKinds.SignedPv
         select d.Id).AnyAsync(ct);
}

public class FinalVisitDocumentDto
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public Guid? ReportId { get; set; }
    public Guid? SnagId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
}

/// <summary>
/// Attaches a file to a reservation's final-visit case: REPORT_PHOTO (with
/// ReportId), SNAG_PHOTO / SNAG_PROOF (with SnagId) or SIGNED_PV.
/// </summary>
public class UploadFinalVisitDocumentCommand : IRequest<FinalVisitDocumentDto>
{
    public Guid ReservationId { get; set; }
    public IFormFile? File { get; set; }
    public string? Kind { get; set; }
    public Guid? ReportId { get; set; }
    public Guid? SnagId { get; set; }
}

public class UploadFinalVisitDocumentHandler : IRequestHandler<UploadFinalVisitDocumentCommand, FinalVisitDocumentDto>
{
    private readonly ApplicationDbContext _db;
    private readonly IBlobStorageService _blobStorage;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;

    public UploadFinalVisitDocumentHandler(ApplicationDbContext db, IBlobStorageService blobStorage, ProjectScopeService projectScope, ICurrentUser currentUser)
    {
        _db = db;
        _blobStorage = blobStorage;
        _projectScope = projectScope;
        _currentUser = currentUser;
    }

    public async Task<FinalVisitDocumentDto> Handle(UploadFinalVisitDocumentCommand request, CancellationToken ct)
    {
        await _projectScope.EnsureReservationAccessAsync(request.ReservationId, ct);

        var kind = request.Kind?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!FinalVisitDocumentKinds.All.Contains(kind))
        {
            throw new BusinessRuleException(BusinessErrorCodes.ValidationFailed,
                $"Type de document inconnu. Valeurs : {string.Join(", ", FinalVisitDocumentKinds.All)}.");
        }

        var visitCase = await _db.Set<FinalVisitCase>().Include(c => c.Appointments)
            .FirstOrDefaultAsync(c => c.ReservationId == request.ReservationId, ct)
            ?? throw new NotFoundException("Aucune visite finale n'existe pour cette réservation.");
        var appointmentIds = visitCase.Appointments.Select(a => a.Id).ToList();

        Guid? reportId = null, snagId = null;
        if (kind is FinalVisitDocumentKinds.ReportPhoto or FinalVisitDocumentKinds.SignedPv)
        {
            // A photo goes with a given report version; the PV is signed on a submitted report.
            var report = await _db.Set<FinalVisitReport>()
                .Where(r => appointmentIds.Contains(r.AppointmentId) && r.Status != ReportStatus.Superseded && r.Status != ReportStatus.Draft)
                .Where(r => request.ReportId == null || r.Id == request.ReportId)
                .OrderByDescending(r => r.SubmittedAt).ThenByDescending(r => r.VersionNo)
                .Select(r => (Guid?)r.Id)
                .FirstOrDefaultAsync(ct);
            if (report is null)
            {
                throw new BusinessRuleException(BusinessErrorCodes.InvalidStatusTransition,
                    "Enregistrez d'abord le compte rendu de la visite.", StatusCodes.Status409Conflict);
            }
            if (kind == FinalVisitDocumentKinds.ReportPhoto) reportId = report;
        }
        else
        {
            if (request.SnagId is not Guid sid)
            {
                throw new Common.Exceptions.ValidationException(new[] { new FluentValidation.Results.ValidationFailure("SnagId", "La réserve concernée est obligatoire.") });
            }
            var belongs = await (from s in _db.Set<Snag>()
                                 join r in _db.Set<FinalVisitReport>() on s.ReportId equals r.Id
                                 where s.Id == sid && appointmentIds.Contains(r.AppointmentId)
                                 select s.Id).AnyAsync(ct);
            if (!belongs) throw new NotFoundException("Réserve introuvable sur cette visite finale.");
            snagId = sid;
        }

        var file = await UploadedFileInspector.InspectAsync(request.File, nameof(request.File), ct);
        var blobName = $"{visitCase.Id}/{Guid.NewGuid()}_{file.FileName}";
        await using (var stream = request.File!.OpenReadStream())
        {
            await _blobStorage.ForContainer(FinalVisitDocumentStorage.ContainerName).UploadAsync(blobName, stream, file.ContentType, ct);
        }

        var document = new FinalVisitDocument
        {
            Id = Guid.NewGuid(),
            CaseId = visitCase.Id,
            ReportId = reportId,
            SnagId = snagId,
            Kind = kind,
            BlobName = blobName,
            FileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.SizeBytes,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = _currentUser.UserId,
        };
        _db.Add(document);
        await _db.SaveChangesAsync(ct);

        return new FinalVisitDocumentDto
        {
            Id = document.Id, Kind = document.Kind, ReportId = document.ReportId, SnagId = document.SnagId,
            FileName = document.FileName, ContentType = document.ContentType, SizeBytes = document.SizeBytes, UploadedAt = document.UploadedAt,
        };
    }
}

/// <summary>Streams a final-visit document to staff of the project or to the buyer of the file.</summary>
public class DownloadFinalVisitDocumentQuery : IRequest<FinalVisitDocumentContent>
{
    public Guid DocumentId { get; set; }
}

public class FinalVisitDocumentContent
{
    public Stream Content { get; set; } = Stream.Null;
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileName { get; set; } = "document";
}

public class DownloadFinalVisitDocumentHandler : IRequestHandler<DownloadFinalVisitDocumentQuery, FinalVisitDocumentContent>
{
    /// <summary>Internal roles that work the delivery file. Technicians (after-sales) do not.</summary>
    private static readonly string[] ReaderRoles =
    {
        RoleCodes.GlobalAdmin, RoleCodes.ProjectAdmin, RoleCodes.SalesAdmin, RoleCodes.SalesAgent, RoleCodes.Notary
    };

    private readonly ApplicationDbContext _db;
    private readonly IBlobStorageService _blobStorage;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;

    public DownloadFinalVisitDocumentHandler(ApplicationDbContext db, IBlobStorageService blobStorage, ProjectScopeService projectScope, ICurrentUser currentUser)
    {
        _db = db;
        _blobStorage = blobStorage;
        _projectScope = projectScope;
        _currentUser = currentUser;
    }

    public async Task<FinalVisitDocumentContent> Handle(DownloadFinalVisitDocumentQuery request, CancellationToken ct)
    {
        var doc = await (from d in _db.Set<FinalVisitDocument>().AsNoTracking()
                         join c in _db.Set<FinalVisitCase>() on d.CaseId equals c.Id
                         where d.Id == request.DocumentId
                         select new { d.BlobName, d.FileName, d.ContentType, c.ReservationId }).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Document {request.DocumentId} not found.");

        var isInternal = _currentUser.Roles.Any(r => RoleCodes.Internal.Contains(r, StringComparer.Ordinal));
        if (isInternal && !_currentUser.Roles.Any(r => ReaderRoles.Contains(r, StringComparer.Ordinal)))
        {
            throw BusinessRuleException.BuyerScopeDenied();
        }
        await _projectScope.EnsureReservationAccessAsync(doc.ReservationId, ct);
        await _projectScope.EnsureBuyerOwnsReservationAsync(doc.ReservationId, ct);

        var (content, contentType, exists) = await _blobStorage.ForContainer(FinalVisitDocumentStorage.ContainerName).DownloadAsync(doc.BlobName, ct);
        if (!exists || content is null) throw new NotFoundException("Le fichier n'est plus disponible.");

        return new FinalVisitDocumentContent { Content = content, ContentType = doc.ContentType ?? contentType ?? "application/octet-stream", FileName = doc.FileName };
    }
}
