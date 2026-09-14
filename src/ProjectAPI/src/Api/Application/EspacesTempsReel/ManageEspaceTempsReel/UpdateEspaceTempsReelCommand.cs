using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Media;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.EspacesTempsReel.ManageEspaceTempsReel;

/// <summary>
/// Corrects a published site video (§7.2).
///
/// Creation existed but nothing could fix a mistyped link afterwards, so the
/// only remedy for a wrong video was to leave it on the public page. Unlike a
/// construction update — which is versioned because it is a published statement
/// about progress — a video entry is a pointer, and correcting the pointer is
/// not rewriting history.
/// </summary>
public class UpdateEspaceTempsReelCommand : IRequest<EspaceTempsReelResponse>
{
    public Guid Id { get; set; }
    public string? VideoLink { get; set; }
    public DateTime? InsertedAt { get; set; }
}

public class EspaceTempsReelResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string VideoLink { get; set; } = string.Empty;
    public DateTime InsertedAt { get; set; }
}

public class UpdateEspaceTempsReelValidator : AbstractValidator<UpdateEspaceTempsReelCommand>
{
    public UpdateEspaceTempsReelValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.VideoLink).NotEmpty().When(x => x.VideoLink is not null);
    }
}

public class UpdateEspaceTempsReelHandler : IRequestHandler<UpdateEspaceTempsReelCommand, EspaceTempsReelResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;
    private readonly MediaUrlPolicy _media;

    public UpdateEspaceTempsReelHandler(
        ApplicationDbContext db,
        ProjectScopeService projectScope,
        MediaUrlPolicy media)
    {
        _db = db;
        _projectScope = projectScope;
        _media = media;
    }

    public async Task<EspaceTempsReelResponse> Handle(UpdateEspaceTempsReelCommand request, CancellationToken ct)
    {
        var video = await _db.Set<EspaceTempsReel>()
            .FirstOrDefaultAsync(v => v.Id == request.Id, ct)
            ?? throw new NotFoundException($"Video entry {request.Id} not found.");

        await _projectScope.EnsureProjectAccessAsync(video.ProjectId, ct);

        if (request.VideoLink is not null && request.VideoLink != video.VideoLink)
        {
            _media.EnsureVideoUrl(request.VideoLink, "VideoLink");
            video.VideoLink = request.VideoLink;
        }

        if (request.InsertedAt.HasValue)
        {
            video.InsertedAt = request.InsertedAt.Value;
        }

        await _db.SaveChangesAsync(ct);

        return new EspaceTempsReelResponse
        {
            Id = video.Id,
            ProjectId = video.ProjectId,
            VideoLink = video.VideoLink,
            InsertedAt = video.InsertedAt
        };
    }
}

/// <summary>
/// Removes a site video from the project's public page (§7.2).
///
/// §9's "business data is never hard-deleted" covers reservations, payments and
/// the dossiers built on them — a catalogue media pointer is presentation, not
/// a business record, and leaving a wrong or obsolete video permanently visible
/// would be the worse outcome. The blob itself is untouched: the file may be
/// referenced elsewhere, and FileController owns its lifecycle.
/// </summary>
public class DeleteEspaceTempsReelCommand : IRequest<Unit>
{
    public Guid Id { get; set; }
}

public class DeleteEspaceTempsReelHandler : IRequestHandler<DeleteEspaceTempsReelCommand, Unit>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public DeleteEspaceTempsReelHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<Unit> Handle(DeleteEspaceTempsReelCommand request, CancellationToken ct)
    {
        var video = await _db.Set<EspaceTempsReel>()
            .FirstOrDefaultAsync(v => v.Id == request.Id, ct)
            ?? throw new NotFoundException($"Video entry {request.Id} not found.");

        await _projectScope.EnsureProjectAccessAsync(video.ProjectId, ct);

        _db.Remove(video);
        await _db.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
