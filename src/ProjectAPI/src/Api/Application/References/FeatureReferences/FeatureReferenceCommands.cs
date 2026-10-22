using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Media;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.References.FeatureReferences;

public sealed record FeatureUsageItem(Guid Id, string Name);
public sealed record FeatureReferenceUsage(List<FeatureUsageItem> Projects, List<FeatureUsageItem> Quartiers);

internal static class FeatureReferenceRules
{
    /// <summary>Field errors as a structured 422 (RFC 9457) instead of an ad-hoc {message} body or a 500 on an over-long value.</summary>
    public static void Validate(string? name, string? description, string? iconUrl, MediaUrlPolicy media)
    {
        var failures = new List<ValidationFailure>();
        if (string.IsNullOrWhiteSpace(name)) failures.Add(new("Name", "Le nom est obligatoire."));
        else if (name.Trim().Length > 150) failures.Add(new("Name", "Le nom ne doit pas dépasser 150 caractères."));
        if (description?.Trim().Length > 2000) failures.Add(new("Description", "La description ne doit pas dépasser 2000 caractères."));
        if (failures.Count > 0) throw new Common.Exceptions.ValidationException(failures);
        if (!string.IsNullOrWhiteSpace(iconUrl)) media.EnsureImageUrl(iconUrl, "IconUrl");
    }

    public static string Scope(string? value) => value?.Trim().ToUpperInvariant() is "PROJECT" or "QUARTIER" or "BOTH" ? value.Trim().ToUpperInvariant() : "BOTH";
}

// GET /api/feature-references
public class GetFeatureReferencesQuery : IRequest<List<FeatureReference>> { }

public class GetFeatureReferencesHandler : IRequestHandler<GetFeatureReferencesQuery, List<FeatureReference>>
{
    private readonly ApplicationDbContext _db;
    public GetFeatureReferencesHandler(ApplicationDbContext db) => _db = db;

    public Task<List<FeatureReference>> Handle(GetFeatureReferencesQuery request, CancellationToken ct) =>
        _db.FeatureReferences.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
}

// GET /api/feature-references/{id}/usage — projects and quartiers hold a copy (no foreign key), so usage is matched by name.
public class GetFeatureReferenceUsageQuery : IRequest<FeatureReferenceUsage>
{
    public Guid Id { get; set; }
}

public class GetFeatureReferenceUsageHandler : IRequestHandler<GetFeatureReferenceUsageQuery, FeatureReferenceUsage>
{
    private readonly ApplicationDbContext _db;
    public GetFeatureReferenceUsageHandler(ApplicationDbContext db) => _db = db;

    public async Task<FeatureReferenceUsage> Handle(GetFeatureReferenceUsageQuery request, CancellationToken ct)
    {
        var row = await _db.FeatureReferences.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new NotFoundException("Atout", request.Id);
        var name = row.Name.Trim().ToLower();

        var projects = await _db.Set<ProjectFeature>().AsNoTracking()
            .Where(f => f.Name.ToLower() == name)
            .Select(f => new { id = f.ProjectId, name = f.Project.Name }).Distinct().OrderBy(x => x.name).ToListAsync(ct);
        var quartiers = await _db.QuartierFeatures.AsNoTracking()
            .Where(f => f.Title.ToLower() == name)
            .Join(_db.Set<Quartier>(), f => f.QuartierId, q => q.Id, (f, q) => new { id = q.Id, name = q.Name })
            .Distinct().OrderBy(x => x.name).ToListAsync(ct);

        return new FeatureReferenceUsage(
            projects.Select(x => new FeatureUsageItem(x.id, x.name)).ToList(),
            quartiers.Select(x => new FeatureUsageItem(x.id, x.name)).ToList());
    }
}

// POST /api/feature-references
public class CreateFeatureReferenceCommand : IRequest<FeatureReference>
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public string? Scope { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CreateFeatureReferenceHandler : IRequestHandler<CreateFeatureReferenceCommand, FeatureReference>
{
    private readonly ApplicationDbContext _db;
    private readonly MediaUrlPolicy _media;
    public CreateFeatureReferenceHandler(ApplicationDbContext db, MediaUrlPolicy media) { _db = db; _media = media; }

    public async Task<FeatureReference> Handle(CreateFeatureReferenceCommand request, CancellationToken ct)
    {
        FeatureReferenceRules.Validate(request.Name, request.Description, request.IconUrl, _media);
        var row = new FeatureReference
        {
            Id = Guid.NewGuid(),
            Name = request.Name!.Trim(),
            Description = request.Description,
            IconUrl = request.IconUrl,
            Scope = FeatureReferenceRules.Scope(request.Scope),
            IsActive = request.IsActive
        };
        _db.Add(row);
        await _db.SaveChangesAsync(ct);
        return row;
    }
}

// PUT /api/feature-references/{id}
public class UpdateFeatureReferenceCommand : IRequest<FeatureReference>
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public string? Scope { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateFeatureReferenceHandler : IRequestHandler<UpdateFeatureReferenceCommand, FeatureReference>
{
    private readonly ApplicationDbContext _db;
    private readonly MediaUrlPolicy _media;
    public UpdateFeatureReferenceHandler(ApplicationDbContext db, MediaUrlPolicy media) { _db = db; _media = media; }

    public async Task<FeatureReference> Handle(UpdateFeatureReferenceCommand request, CancellationToken ct)
    {
        var row = await _db.FeatureReferences.FindAsync([request.Id], ct)
            ?? throw new NotFoundException("Atout", request.Id);
        FeatureReferenceRules.Validate(request.Name, request.Description, request.IconUrl, _media);
        row.Name = request.Name!.Trim();
        row.Description = request.Description?.Trim();
        row.IconUrl = request.IconUrl?.Trim();
        row.Scope = FeatureReferenceRules.Scope(request.Scope);
        row.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        return row;
    }
}

/// <summary>
/// DELETE /api/feature-references/{id} — deletes the atout from the referential only. Projects and quartiers
/// that already use it keep their own copy, so nothing on a public page changes; the UI lists them and asks first.
/// </summary>
public class DeleteFeatureReferenceCommand : IRequest<MediatR.Unit>
{
    public Guid Id { get; set; }
}

public class DeleteFeatureReferenceHandler : IRequestHandler<DeleteFeatureReferenceCommand, MediatR.Unit>
{
    private readonly ApplicationDbContext _db;
    public DeleteFeatureReferenceHandler(ApplicationDbContext db) => _db = db;

    public async Task<MediatR.Unit> Handle(DeleteFeatureReferenceCommand request, CancellationToken ct)
    {
        var row = await _db.FeatureReferences.FindAsync([request.Id], ct)
            ?? throw new NotFoundException("Atout", request.Id);
        _db.FeatureReferences.Remove(row);
        await _db.SaveChangesAsync(ct);
        return MediatR.Unit.Value;
    }
}
