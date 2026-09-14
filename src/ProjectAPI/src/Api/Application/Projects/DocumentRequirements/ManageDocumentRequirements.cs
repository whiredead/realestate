using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Api.Application.Common.Security;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Infrastructure.Context;

namespace ProjectAPI.Api.Application.Projects.DocumentRequirements;

public class ProjectDocumentRequirementResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string LabelFr { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public int SequenceNo { get; set; }
}

/// <summary>
/// §12.1 — the documents this project demands on a reservation before it may be
/// submitted. An empty list is the normal state and means nothing is required.
/// </summary>
public class GetProjectDocumentRequirementsQuery : IRequest<List<ProjectDocumentRequirementResponse>>
{
    public Guid ProjectId { get; set; }
}

public class GetProjectDocumentRequirementsHandler
    : IRequestHandler<GetProjectDocumentRequirementsQuery, List<ProjectDocumentRequirementResponse>>
{
    private readonly ApplicationDbContext _db;

    public GetProjectDocumentRequirementsHandler(ApplicationDbContext db) => _db = db;

    public async Task<List<ProjectDocumentRequirementResponse>> Handle(
        GetProjectDocumentRequirementsQuery request, CancellationToken ct) =>
        await _db.Set<ProjectDocumentRequirement>()
            .Where(r => r.ProjectId == request.ProjectId)
            .OrderBy(r => r.SequenceNo)
            .ThenBy(r => r.LabelFr)
            .Select(r => new ProjectDocumentRequirementResponse
            {
                Id = r.Id,
                ProjectId = r.ProjectId,
                DocumentType = r.DocumentType,
                LabelFr = r.LabelFr,
                IsRequired = r.IsRequired,
                SequenceNo = r.SequenceNo
            })
            .ToListAsync(ct);
}

/// <summary>
/// Adds or updates one requirement (§12.1). Re-sending an existing DocumentType
/// updates that row rather than failing on the unique index — the console's
/// "add" and "edit" are the same gesture from the administrator's point of view.
///
/// This is the ONLY way a project acquires requirements. Nothing seeds them, so
/// enabling this feature changed no existing project's submission rules.
/// </summary>
public class UpsertProjectDocumentRequirementCommand : IRequest<ProjectDocumentRequirementResponse>
{
    public Guid ProjectId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string LabelFr { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
    public int SequenceNo { get; set; }
}

public class UpsertProjectDocumentRequirementValidator : AbstractValidator<UpsertProjectDocumentRequirementCommand>
{
    public UpsertProjectDocumentRequirementValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.DocumentType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LabelFr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SequenceNo).GreaterThanOrEqualTo(0);
    }
}

public class UpsertProjectDocumentRequirementHandler
    : IRequestHandler<UpsertProjectDocumentRequirementCommand, ProjectDocumentRequirementResponse>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;
    private readonly ICurrentUser _currentUser;

    public UpsertProjectDocumentRequirementHandler(
        ApplicationDbContext db,
        ProjectScopeService projectScope,
        ICurrentUser currentUser)
    {
        _db = db;
        _projectScope = projectScope;
        _currentUser = currentUser;
    }

    public async Task<ProjectDocumentRequirementResponse> Handle(
        UpsertProjectDocumentRequirementCommand request, CancellationToken ct)
    {
        var exists = await _db.Set<Project>().AnyAsync(p => p.Id == request.ProjectId, ct);
        if (!exists)
        {
            throw new NotFoundException($"Project {request.ProjectId} not found.");
        }

        await _projectScope.EnsureProjectAccessAsync(request.ProjectId, ct);

        // Normalised so "cin" and "CIN" are one requirement, matching the
        // case-insensitive comparison the checklist does against
        // ReservationDocument.DocumentType.
        var documentType = request.DocumentType.Trim().ToUpperInvariant();

        var requirement = await _db.Set<ProjectDocumentRequirement>()
            .FirstOrDefaultAsync(r => r.ProjectId == request.ProjectId && r.DocumentType == documentType, ct);

        if (requirement is null)
        {
            requirement = new ProjectDocumentRequirement
            {
                Id = Guid.NewGuid(),
                ProjectId = request.ProjectId,
                DocumentType = documentType,
                CreatedAt = DateTime.UtcNow,
                // From the token, never the body.
                CreatedBy = _currentUser.UserId
            };
            _db.Add(requirement);
        }

        requirement.LabelFr = request.LabelFr.Trim();
        requirement.IsRequired = request.IsRequired;
        requirement.SequenceNo = request.SequenceNo;

        await _db.SaveChangesAsync(ct);

        return new ProjectDocumentRequirementResponse
        {
            Id = requirement.Id,
            ProjectId = requirement.ProjectId,
            DocumentType = requirement.DocumentType,
            LabelFr = requirement.LabelFr,
            IsRequired = requirement.IsRequired,
            SequenceNo = requirement.SequenceNo
        };
    }
}

/// <summary>
/// Removes a requirement (§12.1). This is configuration, not business data —
/// deleting it stops future submissions being blocked by it and touches no
/// reservation, no uploaded document and no decision already taken.
/// </summary>
public class DeleteProjectDocumentRequirementCommand : IRequest<Unit>
{
    public Guid Id { get; set; }
}

public class DeleteProjectDocumentRequirementHandler
    : IRequestHandler<DeleteProjectDocumentRequirementCommand, Unit>
{
    private readonly ApplicationDbContext _db;
    private readonly ProjectScopeService _projectScope;

    public DeleteProjectDocumentRequirementHandler(ApplicationDbContext db, ProjectScopeService projectScope)
    {
        _db = db;
        _projectScope = projectScope;
    }

    public async Task<Unit> Handle(DeleteProjectDocumentRequirementCommand request, CancellationToken ct)
    {
        var requirement = await _db.Set<ProjectDocumentRequirement>()
            .FirstOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new NotFoundException($"Document requirement {request.Id} not found.");

        await _projectScope.EnsureProjectAccessAsync(requirement.ProjectId, ct);

        _db.Remove(requirement);
        await _db.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
