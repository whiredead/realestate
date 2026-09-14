namespace ProjectAPI.Domain.Construction.Entities;

/// <summary>
/// A write touched a project that accepts no business mutation any more
/// (FINALISE or SUSPENDED — <see cref="ProjectStatusCodes.IsReadOnly"/>).
/// </summary>
public class ProjectReadOnlyException : Exception
{
    public Guid ProjectId { get; }

    public ProjectReadOnlyException(Guid projectId)
        : base("Ce projet est finalisé : il est en lecture seule et n'accepte plus aucune modification.")
    {
        ProjectId = projectId;
    }
}
