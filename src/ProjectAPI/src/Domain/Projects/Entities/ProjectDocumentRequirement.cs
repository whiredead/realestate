namespace ProjectAPI.Domain.Projects.Entities;

/// <summary>
/// One document a project requires on a reservation before it may be submitted
/// (§12.1 "Document obligatoire manquant").
///
/// The table starts EMPTY and that is load-bearing: "no requirement rows" means
/// "nothing is required", so adding this feature changes the behaviour of
/// exactly zero existing projects. Requirements are opted into per project by
/// an administrator, never inherited, never seeded — a project that was selling
/// fine yesterday must not start rejecting submissions today because a default
/// list appeared underneath it.
///
/// <see cref="DocumentType"/> is matched against
/// <c>ReservationDocument.DocumentType</c>, case-insensitively, so the two
/// vocabularies are the same one.
/// </summary>
public class ProjectDocumentRequirement
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    /// <summary>
    /// Machine key, matched against ReservationDocument.DocumentType (e.g.
    /// "CIN", "CONTRACT"). Unique per project.
    /// </summary>
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>What the agent sees in the upload list.</summary>
    public string LabelFr { get; set; } = string.Empty;

    /// <summary>
    /// False lists the document as expected without blocking submission. The
    /// row still exists so the agent is prompted for it — an optional
    /// requirement is a checklist item, not an absent one.
    /// </summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>Display order in the checklist.</summary>
    public int SequenceNo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Taken from the token, never the request body.</summary>
    public string? CreatedBy { get; set; }

    public Project Project { get; set; } = null!;
}
