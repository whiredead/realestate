using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Media;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Quartiers.Features;

public class QuartierFeatureDto
{
    public Guid Id { get; set; }
    public Guid QuartierId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Image { get; set; }
    public int SequenceNo { get; set; }

    public static QuartierFeatureDto From(QuartierFeature f) => new()
    {
        Id = f.Id,
        QuartierId = f.QuartierId,
        Title = f.Title,
        Description = f.Description,
        Image = f.Image,
        SequenceNo = f.SequenceNo
    };
}

internal static class QuartierFeatureRules
{
    public static void Validate(string? title, string? description, string? image, MediaUrlPolicy media)
    {
        var failures = new List<ValidationFailure>();
        if (string.IsNullOrWhiteSpace(title)) failures.Add(new ValidationFailure("Title", "Le titre est obligatoire."));
        else if (title.Trim().Length > 200) failures.Add(new ValidationFailure("Title", "Le titre ne peut dépasser 200 caractères."));
        if (description is { Length: > 2000 }) failures.Add(new ValidationFailure("Description", "La description ne peut dépasser 2000 caractères."));
        if (failures.Count > 0) throw new Common.Exceptions.ValidationException(failures);

        // The image is optional; when given it must be an acceptable media link.
        if (!string.IsNullOrWhiteSpace(image)) media.EnsureImageUrl(image, "Image");
    }

    public static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

// GET /api/Projects/quartiers/{quartierId}/features
public class GetQuartierFeaturesQuery : IRequest<List<QuartierFeatureDto>>
{
    public Guid QuartierId { get; set; }
}

public class GetQuartierFeaturesHandler : IRequestHandler<GetQuartierFeaturesQuery, List<QuartierFeatureDto>>
{
    private readonly ApplicationDbContext _db;
    public GetQuartierFeaturesHandler(ApplicationDbContext db) => _db = db;

    public async Task<List<QuartierFeatureDto>> Handle(GetQuartierFeaturesQuery request, CancellationToken ct)
    {
        if (!await _db.Set<Quartier>().AnyAsync(q => q.Id == request.QuartierId, ct))
            throw new NotFoundException($"Quartier {request.QuartierId} not found.");

        var rows = await _db.Set<QuartierFeature>()
            .Where(f => f.QuartierId == request.QuartierId)
            .OrderBy(f => f.SequenceNo).ThenBy(f => f.CreatedAt).ThenBy(f => f.Id)
            .ToListAsync(ct);
        return rows.Select(QuartierFeatureDto.From).ToList();
    }
}

// POST /api/Projects/quartiers/{quartierId}/features
public class CreateQuartierFeatureCommand : IRequest<QuartierFeatureDto>
{
    public Guid QuartierId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Image { get; set; }
}

public class CreateQuartierFeatureHandler : IRequestHandler<CreateQuartierFeatureCommand, QuartierFeatureDto>
{
    private readonly ApplicationDbContext _db;
    private readonly MediaUrlPolicy _media;

    public CreateQuartierFeatureHandler(ApplicationDbContext db, MediaUrlPolicy media)
    {
        _db = db;
        _media = media;
    }

    public async Task<QuartierFeatureDto> Handle(CreateQuartierFeatureCommand request, CancellationToken ct)
    {
        if (!await _db.Set<Quartier>().AnyAsync(q => q.Id == request.QuartierId, ct))
            throw new NotFoundException($"Quartier {request.QuartierId} not found.");

        var image = QuartierFeatureRules.Blank(request.Image);
        QuartierFeatureRules.Validate(request.Title, request.Description, image, _media);

        var next = await _db.Set<QuartierFeature>()
            .Where(f => f.QuartierId == request.QuartierId)
            .Select(f => (int?)f.SequenceNo)
            .MaxAsync(ct) ?? 0;

        var feature = new QuartierFeature
        {
            Id = Guid.NewGuid(),
            QuartierId = request.QuartierId,
            Title = request.Title!.Trim(),
            Description = QuartierFeatureRules.Blank(request.Description),
            Image = image,
            SequenceNo = next + 1,
            CreatedAt = DateTime.UtcNow
        };
        _db.Add(feature);
        await _db.SaveChangesAsync(ct);
        return QuartierFeatureDto.From(feature);
    }
}

// PUT /api/Projects/quartier-features/{id}
public class UpdateQuartierFeatureCommand : IRequest<QuartierFeatureDto>
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    /// <summary>Empty string removes the image.</summary>
    public string? Image { get; set; }
}

public class UpdateQuartierFeatureHandler : IRequestHandler<UpdateQuartierFeatureCommand, QuartierFeatureDto>
{
    private readonly ApplicationDbContext _db;
    private readonly MediaUrlPolicy _media;

    public UpdateQuartierFeatureHandler(ApplicationDbContext db, MediaUrlPolicy media)
    {
        _db = db;
        _media = media;
    }

    public async Task<QuartierFeatureDto> Handle(UpdateQuartierFeatureCommand request, CancellationToken ct)
    {
        var feature = await _db.Set<QuartierFeature>().FirstOrDefaultAsync(f => f.Id == request.Id, ct)
            ?? throw new NotFoundException($"Quartier feature {request.Id} not found.");

        var image = QuartierFeatureRules.Blank(request.Image);
        QuartierFeatureRules.Validate(request.Title, request.Description, image, _media);

        feature.Title = request.Title!.Trim();
        feature.Description = QuartierFeatureRules.Blank(request.Description);
        feature.Image = image;
        await _db.SaveChangesAsync(ct);
        return QuartierFeatureDto.From(feature);
    }
}

// DELETE /api/Projects/quartier-features/{id}
public class DeleteQuartierFeatureCommand : IRequest<MediatR.Unit>
{
    public Guid Id { get; set; }
}

public class DeleteQuartierFeatureHandler : IRequestHandler<DeleteQuartierFeatureCommand, MediatR.Unit>
{
    private readonly ApplicationDbContext _db;
    public DeleteQuartierFeatureHandler(ApplicationDbContext db) => _db = db;

    public async Task<MediatR.Unit> Handle(DeleteQuartierFeatureCommand request, CancellationToken ct)
    {
        var feature = await _db.Set<QuartierFeature>().FirstOrDefaultAsync(f => f.Id == request.Id, ct)
            ?? throw new NotFoundException($"Quartier feature {request.Id} not found.");
        _db.Remove(feature);
        await _db.SaveChangesAsync(ct);
        return MediatR.Unit.Value;
    }
}
