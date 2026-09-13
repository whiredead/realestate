using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Media;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Quartiers.UpdateQuartier;

/// <summary>
/// Corrects a quartier (§7.2 catalogue referential).
///
/// A quartier could be created — inline from a project, or directly — and read,
/// but never edited: a typo in the name propagated to every project card in the
/// catalogue with no way back short of creating a duplicate quartier and
/// repointing the projects at it.
///
/// There is deliberately no delete. A quartier is referenced by projects
/// (Project.QuartierId, ON DELETE SET NULL), so removing one silently detaches
/// live projects from their neighbourhood — a destructive edit disguised as
/// tidying reference data. Renaming covers the real cases.
/// </summary>
public class UpdateQuartierCommand : IRequest<QuartierResponse>
{
    public Guid Id { get; set; }

    /// <summary>Null leaves the field unchanged.</summary>
    public string? Name { get; set; }
    public string? Description { get; set; }

    /// <summary>Comma-separated image URLs, matching how the column is stored.</summary>
    public string? Images { get; set; }
}

public class QuartierResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Images { get; set; }

    /// <summary>How many projects sit in this quartier — the console shows the blast radius of a rename.</summary>
    public int ProjectCount { get; set; }
}

public class UpdateQuartierValidator : AbstractValidator<UpdateQuartierCommand>
{
    public UpdateQuartierValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.Description).MaximumLength(4000).When(x => x.Description is not null);
    }
}

public class UpdateQuartierHandler : IRequestHandler<UpdateQuartierCommand, QuartierResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly MediaUrlPolicy _media;

    public UpdateQuartierHandler(ApplicationDbContext db, MediaUrlPolicy media)
    {
        _db = db;
        _media = media;
    }

    public async Task<QuartierResponse> Handle(UpdateQuartierCommand request, CancellationToken ct)
    {
        var quartier = await _db.Set<Quartier>()
            .FirstOrDefaultAsync(q => q.Id == request.Id, ct)
            ?? throw new NotFoundException($"Quartier {request.Id} not found.");

        // A quartier spans projects, so there is no single project perimeter to
        // check against; the endpoint's own RoleGroups.Admins gate is the
        // boundary, as it is for every other cross-project referential.
        if (request.Images is not null && request.Images != quartier.Images)
        {
            _media.EnsureImageUrls(SplitImages(request.Images), "Images", SplitImages(quartier.Images));
            quartier.Images = request.Images;
        }

        quartier.Name = request.Name ?? quartier.Name;
        quartier.Description = request.Description ?? quartier.Description;

        await _db.SaveChangesAsync(ct);

        return new QuartierResponse
        {
            Id = quartier.Id,
            Name = quartier.Name,
            Description = quartier.Description,
            Images = quartier.Images,
            ProjectCount = await _db.Set<Project>().CountAsync(p => p.QuartierId == quartier.Id, ct)
        };
    }

    /// <summary>Quartier.Images is one comma-separated column, mirroring Project.Images.</summary>
    private static IEnumerable<string> SplitImages(string? packed) =>
        string.IsNullOrWhiteSpace(packed)
            ? Enumerable.Empty<string>()
            : packed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
