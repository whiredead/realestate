namespace ProjectAPI.Api.Application.EspacesTempsReel.CreateEspaceTempsReel;

/// <summary>
/// Command to create a new real-time video entry for a project.
/// </summary>
public class CreateEspaceTempsReelCommand : IRequest<CreateEspaceTempsReelResponse>
{
    public Guid ProjectId { get; set; }
    public string VideoLink { get; set; }
    public DateTime? InsertedAt { get; set; }
}
