namespace ProjectAPI.Api.Application.EspacesTempsReel.GetEspaceTempsReelById;


/// <summary>
/// Query to retrieve a specific EspaceTempsReel entry by its unique ID.
/// </summary>
public class GetEspaceTempsReelByIdQuery : IRequest<GetEspaceTempsReelByIdResponse>
{
    /// <summary>
    /// Gets or sets the ID of the EspaceTempsReel entry.
    /// </summary>
    public Guid Id { get; set; }
}