using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Construction.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Construction.PublishConstructionUpdate;

/// <summary>
/// Publishes a progress update (spec §15.2 FR-CON-003/004).
///
/// Published updates are never overwritten: a correction is submitted as a new
/// version referencing the one it supersedes, so a buyer who already saw the
/// original still has an accurate record of what was said.
/// </summary>
public class PublishConstructionUpdateCommand : IRequest<PublishConstructionUpdateResponse>
{
    public Guid ProjectId { get; set; }
    public decimal ProgressPercent { get; set; }
    public string TitleFr { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public string? DescriptionFr { get; set; }
    public string? DescriptionEn { get; set; }
    public List<string>? MediaUrls { get; set; }
    public UpdateVisibility Visibility { get; set; } = UpdateVisibility.Buyers;

    /// <summary>Set when this update corrects an earlier one.</summary>
    public Guid? SupersedesId { get; set; }

    public string? AuthorUserId { get; set; }
}

public class PublishConstructionUpdateResponse
{
    public Guid UpdateId { get; set; }
    public int VersionNo { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class PublishConstructionUpdateHandler
    : IRequestHandler<PublishConstructionUpdateCommand, PublishConstructionUpdateResponse>
{
    private readonly ApplicationDbContext _db;

    public PublishConstructionUpdateHandler(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PublishConstructionUpdateResponse> Handle(
        PublishConstructionUpdateCommand request,
        CancellationToken ct)
    {
        var project = await _db.Set<Project>()
            .FirstOrDefaultAsync(p => p.Id == request.ProjectId, ct)
            ?? throw new NotFoundException($"Project {request.ProjectId} not found.");

        if (request.ProgressPercent is < 0 or > 100)
        {
            throw new BusinessRuleException(
                BusinessErrorCodes.ValidationFailed,
                "La progression doit être comprise entre 0 et 100.");
        }

        var versionNo = 1;
        if (request.SupersedesId is not null)
        {
            var previous = await _db.Set<ConstructionUpdate>()
                .FirstOrDefaultAsync(u => u.Id == request.SupersedesId, ct)
                ?? throw new NotFoundException($"Update {request.SupersedesId} not found.");

            versionNo = previous.VersionNo + 1;
        }

        var update = new ConstructionUpdate
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            VersionNo = versionNo,
            ProgressPercent = request.ProgressPercent,
            TitleFr = request.TitleFr,
            TitleEn = request.TitleEn ?? request.TitleFr,
            DescriptionFr = request.DescriptionFr,
            DescriptionEn = request.DescriptionEn,
            MediaUrls = request.MediaUrls is { Count: > 0 } ? string.Join(",", request.MediaUrls) : null,
            Visibility = request.Visibility,
            SupersedesId = request.SupersedesId,
            AuthorUserId = request.AuthorUserId,
            PublishedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _db.Add(update);

        // Progress declared on a published update also refreshes the project's
        // headline figure, so the public/buyer views stay consistent.
        project.OverAllProgress = request.ProgressPercent;

        await _db.SaveChangesAsync(ct);

        return new PublishConstructionUpdateResponse
        {
            UpdateId = update.Id,
            VersionNo = update.VersionNo,
            Message = versionNo == 1 ? "Mise à jour publiée." : $"Version corrective publiée (v{versionNo})."
        };
    }
}
