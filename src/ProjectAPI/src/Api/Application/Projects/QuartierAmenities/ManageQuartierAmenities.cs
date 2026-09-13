using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Api.Application.Projects.GetQuartierAmenities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Projects.QuartierAmenities;

/// <summary>
/// Write side of the neighbourhood amenities shown on a project's page (§7.2).
///
/// GetQuartierAmenities has existed since the catalogue was built, but nothing
/// could ever create or remove a row: the list was read-only in the API and
/// therefore populated only by whatever seeded the database.
///
/// NOTE on the keying: despite the name, QuartierAmenity carries a ProjectId,
/// not a QuartierId — the amenities are "what is near THIS project", and two
/// projects in the same quartier may legitimately list different ones. That is
/// the shape the data is already in and it is preserved here rather than
/// remodelled, which would mean rewriting every existing row's owner.
/// </summary>
public class AddQuartierAmenityCommand : IRequest<QuartierAmenityResponse>
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Emoji or short glyph token rendered beside the name.</summary>
    public string Icon { get; set; } = string.Empty;
}

public class AddQuartierAmenityValidator : AbstractValidator<AddQuartierAmenityCommand>
{
    public AddQuartierAmenityValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        // Short on purpose: this is a glyph, and the column is not a place to
        // park an <img> tag or a URL.
        RuleFor(x => x.Icon).MaximumLength(16);
    }
}

public class AddQuartierAmenityHandler : IRequestHandler<AddQuartierAmenityCommand, QuartierAmenityResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public AddQuartierAmenityHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<QuartierAmenityResponse> Handle(AddQuartierAmenityCommand request, CancellationToken ct)
    {
        var exists = await _db.Set<Project>().AnyAsync(p => p.Id == request.ProjectId, ct);
        if (!exists)
        {
            throw new NotFoundException($"Project {request.ProjectId} not found.");
        }

        // §6.4 — this writes to a project's public page.
        await _projectScope.EnsureProjectAccessAsync(request.ProjectId, ct);

        var name = request.Name.Trim();

        // Adding "Écoles" twice produces two identical chips on the page with no
        // way to tell them apart; the second add returns the existing row rather
        // than failing, so a double-submit is harmless.
        var existing = await _db.Set<QuartierAmenity>()
            .FirstOrDefaultAsync(a => a.ProjectId == request.ProjectId && a.Name == name, ct);

        if (existing is not null)
        {
            return new QuartierAmenityResponse { Id = existing.Id, Name = existing.Name, Icon = existing.Icon };
        }

        var amenity = new QuartierAmenity
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            Name = name,
            Icon = request.Icon?.Trim() ?? string.Empty
        };

        _db.Add(amenity);
        await _db.SaveChangesAsync(ct);

        return new QuartierAmenityResponse { Id = amenity.Id, Name = amenity.Name, Icon = amenity.Icon };
    }
}

/// <summary>
/// Removes one amenity chip. Presentation data, not a business record — see the
/// same reasoning on DeleteEspaceTempsReelCommand.
/// </summary>
public class RemoveQuartierAmenityCommand : IRequest<Unit>
{
    public Guid Id { get; set; }
}

public class RemoveQuartierAmenityHandler : IRequestHandler<RemoveQuartierAmenityCommand, Unit>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public RemoveQuartierAmenityHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<Unit> Handle(RemoveQuartierAmenityCommand request, CancellationToken ct)
    {
        var amenity = await _db.Set<QuartierAmenity>()
            .FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new NotFoundException($"Amenity {request.Id} not found.");

        await _projectScope.EnsureProjectAccessAsync(amenity.ProjectId, ct);

        _db.Remove(amenity);
        await _db.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
