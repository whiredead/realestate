using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.References.StatusReferences;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.References.ReferenceItems;

// GET /api/reference-items?category=
public class GetReferenceItemsQuery : IRequest<List<ReferenceItem>>
{
    public string? Category { get; set; }
}

public class GetReferenceItemsHandler : IRequestHandler<GetReferenceItemsQuery, List<ReferenceItem>>
{
    private readonly ApplicationDbContext _db;
    public GetReferenceItemsHandler(ApplicationDbContext db) => _db = db;

    public Task<List<ReferenceItem>> Handle(GetReferenceItemsQuery request, CancellationToken ct) =>
        _db.ReferenceItems.AsNoTracking()
            .Where(x => request.Category == null || x.Category == request.Category)
            .OrderBy(x => x.Category).ThenBy(x => x.SortOrder).ThenBy(x => x.Label)
            .ToListAsync(ct);
}

// POST /api/reference-items
public class CreateReferenceItemCommand : IRequest<ReferenceItem>
{
    public string? Category { get; set; }
    public string? Code { get; set; }
    public string? Label { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Tone { get; set; }
}

public class CreateReferenceItemHandler : IRequestHandler<CreateReferenceItemCommand, ReferenceItem>
{
    private readonly ApplicationDbContext _db;
    public CreateReferenceItemHandler(ApplicationDbContext db) => _db = db;

    public async Task<ReferenceItem> Handle(CreateReferenceItemCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Category) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Label))
            throw new Common.Exceptions.ValidationException([new ValidationFailure("Label", "Catégorie, code et libellé sont obligatoires.")]);
        // Status rows are managed by the status referential, never created as plain items.
        if (request.Category.Trim().StartsWith(StatusReferenceRules.CategoryPrefix, StringComparison.OrdinalIgnoreCase))
            throw new Common.Exceptions.ValidationException([new ValidationFailure("Category", "Les statuts sont définis par le système : modifiez leur présentation depuis le référentiel des statuts.")]);

        var item = new ReferenceItem
        {
            Id = Guid.NewGuid(),
            Category = request.Category.Trim().ToUpperInvariant(),
            Code = request.Code.Trim().ToUpperInvariant(),
            Label = request.Label.Trim(),
            Description = request.Description,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            Tone = request.Tone
        };
        _db.Add(item);
        await _db.SaveChangesAsync(ct);
        return item;
    }
}

// PUT /api/reference-items/{id}
public class UpdateReferenceItemCommand : IRequest<ReferenceItem>
{
    public Guid Id { get; set; }
    public string? Label { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateReferenceItemHandler : IRequestHandler<UpdateReferenceItemCommand, ReferenceItem>
{
    private readonly ApplicationDbContext _db;
    public UpdateReferenceItemHandler(ApplicationDbContext db) => _db = db;

    public async Task<ReferenceItem> Handle(UpdateReferenceItemCommand request, CancellationToken ct)
    {
        var item = await _db.ReferenceItems.FindAsync([request.Id], ct)
            ?? throw new NotFoundException(nameof(ReferenceItem), request.Id);
        if (string.IsNullOrWhiteSpace(request.Label))
            throw new Common.Exceptions.ValidationException([new ValidationFailure("Label", "Le libellé est obligatoire.")]);

        item.Label = request.Label.Trim();
        item.Description = request.Description?.Trim();
        item.SortOrder = request.SortOrder;
        item.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
        return item;
    }
}

// DELETE /api/reference-items/{id}
public class DeleteReferenceItemCommand : IRequest<MediatR.Unit>
{
    public Guid Id { get; set; }
}

public class DeleteReferenceItemHandler : IRequestHandler<DeleteReferenceItemCommand, MediatR.Unit>
{
    private readonly ApplicationDbContext _db;
    public DeleteReferenceItemHandler(ApplicationDbContext db) => _db = db;

    public async Task<MediatR.Unit> Handle(DeleteReferenceItemCommand request, CancellationToken ct)
    {
        var item = await _db.ReferenceItems.FindAsync([request.Id], ct)
            ?? throw new NotFoundException(nameof(ReferenceItem), request.Id);
        _db.Remove(item);
        await _db.SaveChangesAsync(ct);
        return MediatR.Unit.Value;
    }
}
