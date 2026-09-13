using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Projects.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Infrastructure.Context;

// MediatR.Unit (its void marker) collides with the domain's Unit entity.
using UnitEntity = ProjectAPI.Domain.Immeubles.Entities.Unit;

namespace ProjectAPI.Api.Application.Common.Reservations;

/// <summary>One line of the submission checklist, as the console renders it.</summary>
public class ReservationDocumentChecklistItem
{
    public string DocumentType { get; set; } = string.Empty;
    public string LabelFr { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsProvided { get; set; }
    public int SequenceNo { get; set; }
}

/// <summary>
/// §12.1 — evaluates the documents a project requires before a reservation may
/// be submitted, and refuses the submission when a required one is missing.
///
/// The rule is defined entirely by the rows in ProjectDocumentRequirements, and
/// that table starts empty: a project with no configured requirements requires
/// nothing and submits exactly as it did before this existed. Nothing here
/// invents a default list.
/// </summary>
public class ReservationDocumentChecklist
{
    private readonly ApplicationDbContext _db;

    public ReservationDocumentChecklist(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Resolves a reservation's project by walking reservation → unit →
    /// immeuble → project. Returns null when the chain is broken, which the
    /// callers treat as "no requirements" rather than failing a submission over
    /// an unrelated data problem.
    /// </summary>
    public async Task<Guid?> ResolveProjectIdAsync(Guid unitId, CancellationToken ct)
    {
        var projectId = await (
            from u in _db.Set<UnitEntity>()
            join im in _db.Set<Immeuble>() on u.ProjectId equals im.Id
            where u.Id == unitId
            select (Guid?)im.ProjectId).FirstOrDefaultAsync(ct);

        return projectId;
    }

    /// <summary>
    /// The labels of the documents a project requires, empty when it requires
    /// none. Used before a reservation exists, where the per-reservation
    /// checklist has nothing to check against.
    /// </summary>
    public async Task<List<string>> RequiredLabelsAsync(Guid projectId, CancellationToken ct) =>
        await _db.Set<ProjectDocumentRequirement>()
            .Where(r => r.ProjectId == projectId && r.IsRequired)
            .OrderBy(r => r.SequenceNo)
            .Select(r => r.LabelFr)
            .ToListAsync(ct);

    /// <summary>
    /// The checklist for one reservation: every requirement the project
    /// declares, flagged with whether this reservation already carries it.
    /// </summary>
    public async Task<List<ReservationDocumentChecklistItem>> BuildAsync(
        Guid reservationId, Guid unitId, CancellationToken ct)
    {
        var projectId = await ResolveProjectIdAsync(unitId, ct);
        if (projectId is null) return new List<ReservationDocumentChecklistItem>();

        var requirements = await _db.Set<ProjectDocumentRequirement>()
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.SequenceNo)
            .ThenBy(r => r.LabelFr)
            .ToListAsync(ct);

        if (requirements.Count == 0) return new List<ReservationDocumentChecklistItem>();

        var provided = await _db.Set<ReservationDocument>()
            .Where(d => d.ReservationId == reservationId && d.DocumentType != null)
            .Select(d => d.DocumentType!)
            .ToListAsync(ct);

        var providedSet = new HashSet<string>(provided, StringComparer.OrdinalIgnoreCase);

        return requirements.Select(r => new ReservationDocumentChecklistItem
        {
            DocumentType = r.DocumentType,
            LabelFr = r.LabelFr,
            IsRequired = r.IsRequired,
            IsProvided = providedSet.Contains(r.DocumentType),
            SequenceNo = r.SequenceNo
        }).ToList();
    }

    /// <summary>
    /// Throws 422 MISSING_REQUIRED_DOCUMENT when the project requires a
    /// document this reservation does not carry. Silent when the project
    /// declares no requirements.
    /// </summary>
    public async Task EnsureSubmittableAsync(Guid reservationId, Guid unitId, CancellationToken ct)
    {
        var checklist = await BuildAsync(reservationId, unitId, ct);

        var missing = checklist
            .Where(item => item.IsRequired && !item.IsProvided)
            .Select(item => item.LabelFr)
            .ToList();

        if (missing.Count > 0)
        {
            throw BusinessRuleException.MissingRequiredDocument(string.Join(", ", missing));
        }
    }
}
